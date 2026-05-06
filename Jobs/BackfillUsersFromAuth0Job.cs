using Auth0.AuthenticationApi;
using Auth0.AuthenticationApi.Models;
using Auth0.Core.Exceptions;
using Auth0.ManagementApi;
using GAToolAPI.Services;
using GAToolAPI.Services.Auth;

namespace GAToolAPI.Jobs;

/// <summary>
///     One-shot migration job: enumerates every user in Auth0 (email connection only)
///     and upserts them into the new <c>gatool-auth</c> DynamoDB table with the
///     equivalent role set. Designed to be run once when cutting over from Auth0
///     to the self-hosted auth system, but is idempotent — re-running it just
///     re-syncs roles for any users whose Auth0 roles have changed since the
///     last run.
///
///     Role mapping (Auth0 → gatool):
///       - Any role whose name contains "admin" (case-insensitive) → grants "admin" + "user"
///       - The "Full User" role (id <c>rol_KRLODHx3eNItUgvI</c>) → grants "user"
///       - Any role whose name contains "user" (case-insensitive) → grants "user"
///       - Read-only / no relevant roles → user is upserted with an empty role array
///         (they exist in DDB but won't satisfy the [user] / [admin] authorization
///         policies, matching their current Auth0 behavior).
///
///     Run with: <c>dotnet run -- --job BackfillUsersFromAuth0</c>
///     Dry run:  <c>DryRun=true dotnet run -- --job BackfillUsersFromAuth0</c>
/// </summary>
public class BackfillUsersFromAuth0Job(
    ILogger<BackfillUsersFromAuth0Job> logger,
    IConfiguration configuration,
    ISecretProvider secretProvider,
    AuthRepository authRepository) : IJob
{
    private const string Auth0Domain = "gatool.auth0.com";
    private const string FullUserRoleId = "rol_KRLODHx3eNItUgvI";
    private const int PageSize = 50;
    // Auth0 free tier limits the Management API to ~2 req/s. We throttle every
    // call and back off on 429 to stay well under that.
    private static readonly TimeSpan PerCallDelay = TimeSpan.FromMilliseconds(600);
    private static readonly TimeSpan RateLimitBackoff = TimeSpan.FromSeconds(10);
    private const int MaxRetriesPerCall = 5;

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var isDryRun = configuration.GetValue<bool>("DryRun");
        var prefix = isDryRun ? "[DRY RUN] " : "";
        logger.LogInformation("{Prefix}Starting BackfillUsersFromAuth0 job", prefix);

        // Mint a Management API token using the same client credentials the
        // Mailchimp webhook uses for its dual-write.
        var clientId = await secretProvider.GetSecretAsync("Auth0AdminClientId", cancellationToken);
        var clientSecret = await secretProvider.GetSecretAsync("Auth0AdminClientSecret", cancellationToken);

        var authClient = new AuthenticationApiClient(Auth0Domain);
        var tokenResp = await authClient.GetTokenAsync(new ClientCredentialsTokenRequest
        {
            ClientId = clientId,
            ClientSecret = clientSecret,
            Audience = $"https://{Auth0Domain}/api/v2/"
        }, cancellationToken);
        var mgmt = new ManagementApiClient(tokenResp.AccessToken, Auth0Domain);

        // Strategy: instead of N+1 calls (1 list + GetRoles per user), we list
        // roles once and then page through each role's *members*. That's
        // O(roles × pages-of-members) calls — typically a few dozen — versus
        // thousands. Auth0 free tier is 2 req/s so the per-user approach was
        // hopelessly slow even with throttling.
        var allRoles = await CallWithRetryAsync(() => mgmt.Roles.GetAllAsync(
            new Auth0.ManagementApi.Models.GetRolesRequest(),
            new Auth0.ManagementApi.Paging.PaginationInfo(0, 50, includeTotals: false),
            cancellationToken), cancellationToken);
        logger.LogInformation("Found {Count} Auth0 roles: [{Names}]",
            allRoles.Count, string.Join(", ", allRoles.Select(r => r.Name)));

        // email → set of gatool roles, accumulated across role lookups.
        var emailToRoles = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var role in allRoles)
        {
            var name = role.Name ?? "";
            string? mappedRole = null;
            bool grantsUser = false;
            if (name.Contains("admin", StringComparison.OrdinalIgnoreCase))
            {
                mappedRole = "admin";
                grantsUser = true;
            }
            else if (role.Id == FullUserRoleId || name.Contains("user", StringComparison.OrdinalIgnoreCase))
            {
                mappedRole = "user";
            }

            if (mappedRole == null)
            {
                logger.LogInformation("Role '{Name}' ({Id}): not mapped; skipping members", name, role.Id);
                continue;
            }

            int memberPage = 0;
            int memberCount = 0;
            while (true)
            {
                var members = await CallWithRetryAsync(() => mgmt.Roles.GetUsersAsync(role.Id,
                    new Auth0.ManagementApi.Paging.PaginationInfo(memberPage, PageSize, includeTotals: false),
                    cancellationToken), cancellationToken);
                if (members.Count == 0) break;
                foreach (var m in members)
                {
                    if (string.IsNullOrWhiteSpace(m.Email)) continue;
                    if (!emailToRoles.TryGetValue(m.Email, out var set))
                        emailToRoles[m.Email] = set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    set.Add(mappedRole);
                    if (grantsUser) set.Add("user");
                    memberCount++;
                }
                memberPage++;
            }
            logger.LogInformation("Role '{Name}' → {Mapped}: {Count} members", name, mappedRole, memberCount);
        }

        logger.LogInformation("Collected {Count} unique users with mapped roles; upserting to DDB",
            emailToRoles.Count);

        int totalCreated = 0, totalUpdated = 0, totalUnchanged = 0, totalFailed = 0;
        foreach (var (email, roleSet) in emailToRoles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var roles = roleSet.OrderBy(r => r).ToArray();
            try
            {
                if (isDryRun)
                {
                    logger.LogInformation("{Prefix}Would upsert {Email} with roles [{Roles}]",
                        prefix, email, string.Join(",", roles));
                    continue;
                }

                var existing = await authRepository.GetUserAsync(email, cancellationToken);
                if (existing == null)
                {
                    await authRepository.UpsertUserAsync(email, roles, cancellationToken);
                    totalCreated++;
                    logger.LogInformation("Created {Email} with roles [{Roles}]",
                        email, string.Join(",", roles));
                }
                else if (!existing.Roles.OrderBy(r => r).SequenceEqual(roles))
                {
                    await authRepository.SetRolesAsync(email, roles, cancellationToken);
                    totalUpdated++;
                    logger.LogInformation("Updated {Email} roles [{Old}] → [{New}]",
                        email, string.Join(",", existing.Roles), string.Join(",", roles));
                }
                else
                {
                    totalUnchanged++;
                }
            }
            catch (Exception ex)
            {
                totalFailed++;
                logger.LogError(ex, "Backfill failed for {Email}", email);
            }
        }

        logger.LogInformation(
            "{Prefix}Backfill complete. users={Users} created={Created} updated={Updated} unchanged={Unchanged} failed={Failed}",
            prefix, emailToRoles.Count, totalCreated, totalUpdated, totalUnchanged, totalFailed);
    }

    private async Task<T> CallWithRetryAsync<T>(Func<Task<T>> call, CancellationToken ct)
    {
        // Pre-throttle every call so we stay under 2 req/s on Auth0's free tier.
        await Task.Delay(PerCallDelay, ct);
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                return await call();
            }
            catch (RateLimitApiException) when (attempt < MaxRetriesPerCall)
            {
                var backoff = TimeSpan.FromSeconds(RateLimitBackoff.TotalSeconds * attempt);
                logger.LogWarning("Auth0 rate-limited (attempt {Attempt}/{Max}); backing off {Backoff}s",
                    attempt, MaxRetriesPerCall, backoff.TotalSeconds);
                await Task.Delay(backoff, ct);
            }
        }
    }

}
