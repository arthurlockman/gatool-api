using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using GAToolAPI.Models;

namespace GAToolAPI.Services.Auth;

/// <summary>
///     DynamoDB access layer for the gatool-auth single-table design.
///
///     PK / SK patterns:
///       USER#{email}          / PROFILE                — UserRecord
///       USER#{email}          / PASSKEY#{credentialId} — PasskeyRecord
///       OTP#{email}           / CODE                   — OtpRecord (TTL ~10 min)
///       REFRESH#{tokenHash}   / TOKEN                  — RefreshTokenRecord (TTL ~30 days)
///       PASSKEY-LOOKUP        / {credentialId}         — credentialId -> email index row
///
///     TTL attribute name is "expiresAt" (epoch seconds).
/// </summary>
public class AuthRepository
{
    private const string TableName = "gatool-auth";
    private const string TtlAttribute = "expiresAt";

    private readonly IAmazonDynamoDB _ddb;
    private readonly ILogger<AuthRepository> _logger;

    public AuthRepository(IAmazonDynamoDB ddb, ILogger<AuthRepository> logger)
    {
        _ddb = ddb;
        _logger = logger;
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    // ── Users ────────────────────────────────────────────────────────────────

    public async Task<UserRecord?> GetUserAsync(string email, CancellationToken ct = default)
    {
        var resp = await _ddb.GetItemAsync(new GetItemRequest
        {
            TableName = TableName,
            Key = Pk($"USER#{NormalizeEmail(email)}", "PROFILE"),
            ConsistentRead = true
        }, ct);

        return resp.Item is { Count: > 0 } ? UserFromItem(resp.Item) : null;
    }

    public async Task<UserRecord> UpsertUserAsync(string email, string[]? rolesIfNew = null,
        CancellationToken ct = default)
    {
        var normalized = NormalizeEmail(email);
        var existing = await GetUserAsync(normalized, ct);
        if (existing != null) return existing;

        var record = new UserRecord
        {
            Email = normalized,
            Roles = rolesIfNew ?? ["user"],
            CreatedAt = DateTimeOffset.UtcNow
        };

        try
        {
            await _ddb.PutItemAsync(new PutItemRequest
            {
                TableName = TableName,
                Item = UserToItem(record),
                ConditionExpression = "attribute_not_exists(PK)"
            }, ct);
            _logger.LogInformation("Created auth user record for {Email}", normalized);
        }
        catch (ConditionalCheckFailedException)
        {
            // Race: another caller created it concurrently. Re-read.
            return await GetUserAsync(normalized, ct) ?? record;
        }
        return record;
    }

    public async Task SetRolesAsync(string email, string[] roles, CancellationToken ct = default)
    {
        var normalized = NormalizeEmail(email);

        // Ensure the record exists first
        await UpsertUserAsync(normalized, roles, ct);

        var rolesAttr = new AttributeValue { L = roles.Select(r => new AttributeValue { S = r }).ToList() };
        await _ddb.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = TableName,
            Key = Pk($"USER#{normalized}", "PROFILE"),
            UpdateExpression = "SET #r = :r",
            ExpressionAttributeNames = new Dictionary<string, string> { ["#r"] = "roles" },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":r"] = rolesAttr }
        }, ct);
    }

    public async Task TouchLoginAsync(string email, CancellationToken ct = default)
    {
        var normalized = NormalizeEmail(email);
        await _ddb.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = TableName,
            Key = Pk($"USER#{normalized}", "PROFILE"),
            UpdateExpression = "SET lastLoginAt = :t",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":t"] = new() { S = DateTimeOffset.UtcNow.ToString("O") }
            }
        }, ct);
    }

    public async Task DeleteUserAsync(string email, CancellationToken ct = default)
    {
        var normalized = NormalizeEmail(email);

        var passkeys = await ListPasskeysAsync(normalized, ct);

        // Delete profile + each passkey + each lookup row.
        // BatchWriteItem caps at 25 per call.
        var deletes = new List<WriteRequest>
        {
            new() { DeleteRequest = new DeleteRequest { Key = Pk($"USER#{normalized}", "PROFILE") } }
        };
        foreach (var p in passkeys)
        {
            deletes.Add(new WriteRequest
            {
                DeleteRequest = new DeleteRequest { Key = Pk($"USER#{normalized}", $"PASSKEY#{p.CredentialId}") }
            });
            deletes.Add(new WriteRequest
            {
                DeleteRequest = new DeleteRequest { Key = Pk("PASSKEY-LOOKUP", p.CredentialId) }
            });
        }

        foreach (var chunk in deletes.Chunk(25))
        {
            await _ddb.BatchWriteItemAsync(new BatchWriteItemRequest
            {
                RequestItems = new Dictionary<string, List<WriteRequest>> { [TableName] = chunk.ToList() }
            }, ct);
        }

        _logger.LogInformation("Deleted auth user record (and {Count} passkeys) for {Email}",
            passkeys.Count, normalized);
    }

    // ── Passkeys ────────────────────────────────────────────────────────────

    public async Task<List<PasskeyRecord>> ListPasskeysAsync(string email, CancellationToken ct = default)
    {
        var normalized = NormalizeEmail(email);
        var resp = await _ddb.QueryAsync(new QueryRequest
        {
            TableName = TableName,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :sk)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new() { S = $"USER#{normalized}" },
                [":sk"] = new() { S = "PASSKEY#" }
            }
        }, ct);

        return resp.Items.Select(PasskeyFromItem).ToList();
    }

    /// <summary>
    /// Look up a passkey by credentialId across all users (used during authentication
    /// when the browser sends a credentialId before the user has identified themselves).
    /// Uses a denormalized PASSKEY-LOOKUP row.
    /// </summary>
    public async Task<PasskeyRecord?> GetPasskeyByCredentialIdAsync(string credentialId,
        CancellationToken ct = default)
    {
        var lookup = await _ddb.GetItemAsync(new GetItemRequest
        {
            TableName = TableName,
            Key = Pk("PASSKEY-LOOKUP", credentialId),
            ConsistentRead = true
        }, ct);
        if (lookup.Item is not { Count: > 0 } || !lookup.Item.TryGetValue("email", out var e))
            return null;

        var passkey = await _ddb.GetItemAsync(new GetItemRequest
        {
            TableName = TableName,
            Key = Pk($"USER#{e.S}", $"PASSKEY#{credentialId}"),
            ConsistentRead = true
        }, ct);
        return passkey.Item is { Count: > 0 } ? PasskeyFromItem(passkey.Item) : null;
    }

    public async Task SavePasskeyAsync(PasskeyRecord passkey, CancellationToken ct = default)
    {
        passkey.Email = NormalizeEmail(passkey.Email);

        await _ddb.TransactWriteItemsAsync(new TransactWriteItemsRequest
        {
            TransactItems =
            [
                new TransactWriteItem
                {
                    Put = new Put
                    {
                        TableName = TableName,
                        Item = PasskeyToItem(passkey),
                        // Reject if this credentialId is already registered for this user
                        ConditionExpression = "attribute_not_exists(PK)"
                    }
                },
                new TransactWriteItem
                {
                    Put = new Put
                    {
                        TableName = TableName,
                        Item = new Dictionary<string, AttributeValue>
                        {
                            ["PK"] = new() { S = "PASSKEY-LOOKUP" },
                            ["SK"] = new() { S = passkey.CredentialId },
                            ["email"] = new() { S = passkey.Email }
                        },
                        // Reject if a different user already claimed this credentialId
                        ConditionExpression = "attribute_not_exists(PK)"
                    }
                }
            ]
        }, ct);
    }

    public async Task UpdatePasskeyCounterAsync(string email, string credentialId, uint signCount,
        CancellationToken ct = default)
    {
        var normalized = NormalizeEmail(email);
        await _ddb.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = TableName,
            Key = Pk($"USER#{normalized}", $"PASSKEY#{credentialId}"),
            UpdateExpression = "SET signCount = :c, lastUsedAt = :t",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":c"] = new() { N = signCount.ToString() },
                [":t"] = new() { S = DateTimeOffset.UtcNow.ToString("O") }
            }
        }, ct);
    }

    public async Task DeletePasskeyAsync(string email, string credentialId, CancellationToken ct = default)
    {
        var normalized = NormalizeEmail(email);
        await _ddb.TransactWriteItemsAsync(new TransactWriteItemsRequest
        {
            TransactItems =
            [
                new TransactWriteItem
                {
                    Delete = new Delete
                    {
                        TableName = TableName,
                        Key = Pk($"USER#{normalized}", $"PASSKEY#{credentialId}")
                    }
                },
                new TransactWriteItem
                {
                    Delete = new Delete
                    {
                        TableName = TableName,
                        Key = Pk("PASSKEY-LOOKUP", credentialId)
                    }
                }
            ]
        }, ct);
    }

    // ── OTP codes ───────────────────────────────────────────────────────────

    public async Task SaveOtpAsync(OtpRecord record, CancellationToken ct = default)
    {
        record.Email = NormalizeEmail(record.Email);
        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new() { S = $"OTP#{record.Email}" },
            ["SK"] = new() { S = "CODE" },
            ["codeHash"] = new() { S = record.CodeHash },
            ["createdAt"] = new() { S = record.CreatedAt.ToString("O") },
            ["attemptsRemaining"] = new() { N = record.AttemptsRemaining.ToString() },
            [TtlAttribute] = new() { N = record.ExpiresAt.ToUnixTimeSeconds().ToString() }
        };
        await _ddb.PutItemAsync(new PutItemRequest { TableName = TableName, Item = item }, ct);
    }

    public async Task<OtpRecord?> GetOtpAsync(string email, CancellationToken ct = default)
    {
        var normalized = NormalizeEmail(email);
        var resp = await _ddb.GetItemAsync(new GetItemRequest
        {
            TableName = TableName,
            Key = Pk($"OTP#{normalized}", "CODE"),
            ConsistentRead = true
        }, ct);
        if (resp.Item is not { Count: > 0 }) return null;

        var record = new OtpRecord
        {
            Email = normalized,
            CodeHash = resp.Item.GetValueOrDefault("codeHash")?.S ?? "",
            CreatedAt = ParseDate(resp.Item.GetValueOrDefault("createdAt")?.S),
            AttemptsRemaining = int.Parse(resp.Item.GetValueOrDefault("attemptsRemaining")?.N ?? "0"),
            ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(
                long.Parse(resp.Item.GetValueOrDefault(TtlAttribute)?.N ?? "0"))
        };
        // Defensive: TTL only deletes within ~48 hours, so we double-check expiry on read.
        if (record.ExpiresAt < DateTimeOffset.UtcNow) return null;
        return record;
    }

    public async Task DecrementOtpAttemptsAsync(string email, CancellationToken ct = default)
    {
        var normalized = NormalizeEmail(email);
        try
        {
            await _ddb.UpdateItemAsync(new UpdateItemRequest
            {
                TableName = TableName,
                Key = Pk($"OTP#{normalized}", "CODE"),
                UpdateExpression = "SET attemptsRemaining = attemptsRemaining - :one",
                ConditionExpression = "attemptsRemaining > :zero",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":one"] = new() { N = "1" },
                    [":zero"] = new() { N = "0" }
                }
            }, ct);
        }
        catch (ConditionalCheckFailedException)
        {
            // Already at 0 — delete it
            await DeleteOtpAsync(normalized, ct);
        }
    }

    public async Task DeleteOtpAsync(string email, CancellationToken ct = default)
    {
        var normalized = NormalizeEmail(email);
        await _ddb.DeleteItemAsync(new DeleteItemRequest
        {
            TableName = TableName,
            Key = Pk($"OTP#{normalized}", "CODE")
        }, ct);
    }

    // ── Refresh tokens ──────────────────────────────────────────────────────

    public async Task SaveRefreshTokenAsync(RefreshTokenRecord token, CancellationToken ct = default)
    {
        token.Email = NormalizeEmail(token.Email);
        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new() { S = $"REFRESH#{token.TokenHash}" },
            ["SK"] = new() { S = "TOKEN" },
            ["email"] = new() { S = token.Email },
            ["createdAt"] = new() { S = token.CreatedAt.ToString("O") },
            [TtlAttribute] = new() { N = token.ExpiresAt.ToUnixTimeSeconds().ToString() }
        };
        if (!string.IsNullOrEmpty(token.UserAgent))
            item["userAgent"] = new AttributeValue { S = token.UserAgent };

        await _ddb.PutItemAsync(new PutItemRequest { TableName = TableName, Item = item }, ct);
    }

    public async Task<RefreshTokenRecord?> GetRefreshTokenAsync(string tokenHash, CancellationToken ct = default)
    {
        var resp = await _ddb.GetItemAsync(new GetItemRequest
        {
            TableName = TableName,
            Key = Pk($"REFRESH#{tokenHash}", "TOKEN"),
            ConsistentRead = true
        }, ct);
        if (resp.Item is not { Count: > 0 }) return null;

        var record = new RefreshTokenRecord
        {
            TokenHash = tokenHash,
            Email = resp.Item.GetValueOrDefault("email")?.S ?? "",
            CreatedAt = ParseDate(resp.Item.GetValueOrDefault("createdAt")?.S),
            UserAgent = resp.Item.GetValueOrDefault("userAgent")?.S,
            ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(
                long.Parse(resp.Item.GetValueOrDefault(TtlAttribute)?.N ?? "0"))
        };
        if (record.ExpiresAt < DateTimeOffset.UtcNow) return null;
        return record;
    }

    public async Task DeleteRefreshTokenAsync(string tokenHash, CancellationToken ct = default)
    {
        await _ddb.DeleteItemAsync(new DeleteItemRequest
        {
            TableName = TableName,
            Key = Pk($"REFRESH#{tokenHash}", "TOKEN")
        }, ct);
    }

    /// <summary>
    /// Atomically consume a refresh token (delete it only if it still exists).
    /// Returns true on the first successful consumption, false if the token was already
    /// deleted by another caller (replay or concurrent refresh).
    /// </summary>
    public async Task<bool> TryConsumeRefreshTokenAsync(string tokenHash, CancellationToken ct = default)
    {
        try
        {
            await _ddb.DeleteItemAsync(new DeleteItemRequest
            {
                TableName = TableName,
                Key = Pk($"REFRESH#{tokenHash}", "TOKEN"),
                ConditionExpression = "attribute_exists(PK)"
            }, ct);
            return true;
        }
        catch (ConditionalCheckFailedException)
        {
            return false;
        }
    }

    /// <summary>
    /// Atomically consume an OTP record only if the supplied codeHash still matches.
    /// Used after a successful in-memory hash comparison to prevent the same code from
    /// being redeemed twice under concurrent verification attempts.
    /// </summary>
    public async Task<bool> TryConsumeOtpAsync(string email, string expectedCodeHash,
        CancellationToken ct = default)
    {
        var normalized = NormalizeEmail(email);
        try
        {
            await _ddb.DeleteItemAsync(new DeleteItemRequest
            {
                TableName = TableName,
                Key = Pk($"OTP#{normalized}", "CODE"),
                ConditionExpression = "codeHash = :h",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":h"] = new() { S = expectedCodeHash }
                }
            }, ct);
            return true;
        }
        catch (ConditionalCheckFailedException)
        {
            return false;
        }
    }

    // ── Item <-> POCO mappers ───────────────────────────────────────────────

    private static Dictionary<string, AttributeValue> UserToItem(UserRecord r) => new()
    {
        ["PK"] = new() { S = $"USER#{r.Email}" },
        ["SK"] = new() { S = "PROFILE" },
        ["email"] = new() { S = r.Email },
        ["roles"] = new() { L = r.Roles.Select(role => new AttributeValue { S = role }).ToList() },
        ["createdAt"] = new() { S = r.CreatedAt.ToString("O") }
    };

    private static UserRecord UserFromItem(Dictionary<string, AttributeValue> item) => new()
    {
        Email = item.GetValueOrDefault("email")?.S ?? "",
        Roles = item.GetValueOrDefault("roles")?.L?.Select(av => av.S).ToArray() ?? ["user"],
        CreatedAt = ParseDate(item.GetValueOrDefault("createdAt")?.S),
        LastLoginAt = item.TryGetValue("lastLoginAt", out var ll) && ll.S != null ? ParseDate(ll.S) : null
    };

    private static Dictionary<string, AttributeValue> PasskeyToItem(PasskeyRecord r)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new() { S = $"USER#{r.Email}" },
            ["SK"] = new() { S = $"PASSKEY#{r.CredentialId}" },
            ["email"] = new() { S = r.Email },
            ["credentialId"] = new() { S = r.CredentialId },
            ["publicKey"] = new() { B = new MemoryStream(r.PublicKey) },
            ["signCount"] = new() { N = r.SignCount.ToString() },
            ["aaGuid"] = new() { S = r.AaGuid.ToString() },
            ["createdAt"] = new() { S = r.CreatedAt.ToString("O") }
        };
        if (r.Transports.Length > 0)
            item["transports"] = new AttributeValue
            {
                L = r.Transports.Select(t => new AttributeValue { S = t }).ToList()
            };
        if (!string.IsNullOrEmpty(r.Nickname))
            item["nickname"] = new AttributeValue { S = r.Nickname };
        if (r.LastUsedAt.HasValue)
            item["lastUsedAt"] = new AttributeValue { S = r.LastUsedAt.Value.ToString("O") };
        return item;
    }

    private static PasskeyRecord PasskeyFromItem(Dictionary<string, AttributeValue> item)
    {
        var pkBytes = item.TryGetValue("publicKey", out var pkAv) && pkAv.B != null
            ? pkAv.B.ToArray()
            : [];
        return new PasskeyRecord
        {
            Email = item.GetValueOrDefault("email")?.S ?? "",
            CredentialId = item.GetValueOrDefault("credentialId")?.S ?? "",
            PublicKey = pkBytes,
            SignCount = uint.Parse(item.GetValueOrDefault("signCount")?.N ?? "0"),
            AaGuid = Guid.TryParse(item.GetValueOrDefault("aaGuid")?.S, out var g) ? g : Guid.Empty,
            Nickname = item.GetValueOrDefault("nickname")?.S,
            CreatedAt = ParseDate(item.GetValueOrDefault("createdAt")?.S),
            LastUsedAt = item.TryGetValue("lastUsedAt", out var lu) && lu.S != null ? ParseDate(lu.S) : null,
            Transports = item.GetValueOrDefault("transports")?.L?
                .Where(av => av.S != null).Select(av => av.S).ToArray() ?? []
        };
    }

    private static Dictionary<string, AttributeValue> Pk(string pk, string sk) => new()
    {
        ["PK"] = new() { S = pk },
        ["SK"] = new() { S = sk }
    };

    private static DateTimeOffset ParseDate(string? s) =>
        DateTimeOffset.TryParse(s, out var d) ? d : DateTimeOffset.MinValue;
}
