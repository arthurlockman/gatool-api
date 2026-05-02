using System.Threading.RateLimiting;
using Auth0.AuthenticationApi;
using Auth0.AuthenticationApi.Models;
using Auth0.ManagementApi;
using Auth0.ManagementApi.Models;
using GAToolAPI.Services.Auth;
using MailChimp.Net;
using MailChimp.Net.Core;

namespace GAToolAPI.Services;

/// <summary>
/// Handles Mailchimp subscribe/unsubscribe/profile webhooks.
///
/// During the Auth0 -> custom-auth migration we mirror role/user changes
/// to BOTH systems so existing Auth0-issued tokens (still in flight) and
/// the new gatool-issued tokens see consistent role state. Once every
/// client has cut over to the new auth flow, the Auth0 side can be
/// removed (along with the Auth0 packages and admin secrets) by the
/// `cleanup-auth0` task.
/// </summary>
public class MailchimpWebhookService
{
    private const string OptInText =
        "I want access to gatool and agree that I will not abuse this access to team data.";

    // Auth0 (legacy, kept until cutover)
    private const string Auth0Domain = "gatool.auth0.com";
    private const string FullUserRoleId = "rol_KRLODHx3eNItUgvI";
    private const string ReadOnlyRoleId = "rol_EQcREtmOWaGanRYG";

    private const string WelcomeTag = "gatool-welcome";

    private readonly ILogger<MailchimpWebhookService> _logger;
    private readonly ISecretProvider _secretProvider;
    private readonly UserStorageService _userStorageService;
    private readonly AuthRepository _authRepository;

    private readonly SlidingWindowRateLimiter _mailchimpRateLimiter = new(new SlidingWindowRateLimiterOptions
    {
        AutoReplenishment = true,
        Window = TimeSpan.FromSeconds(1),
        PermitLimit = 5,
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        SegmentsPerWindow = 1,
        QueueLimit = int.MaxValue
    });

    private readonly SlidingWindowRateLimiter _auth0RateLimiter = new(new SlidingWindowRateLimiterOptions
    {
        AutoReplenishment = true,
        Window = TimeSpan.FromSeconds(1),
        PermitLimit = 2,
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        SegmentsPerWindow = 1,
        QueueLimit = int.MaxValue
    });

    private MailChimpManager? _mailChimpClient;
    private string? _mailChimpListId;

    // Auth0 management client — lazy-initialized & token-refreshed
    private ManagementApiClient? _auth0Client;
    private DateTimeOffset _auth0TokenExpiresAt = DateTimeOffset.MinValue;
    private string? _auth0ClientId;
    private string? _auth0ClientSecret;
    private readonly SemaphoreSlim _auth0TokenLock = new(1, 1);
    private static readonly TimeSpan Auth0TokenRefreshSkew = TimeSpan.FromMinutes(5);

    public MailchimpWebhookService(
        ILogger<MailchimpWebhookService> logger,
        ISecretProvider secretProvider,
        UserStorageService userStorageService,
        AuthRepository authRepository)
    {
        _logger = logger;
        _secretProvider = secretProvider;
        _userStorageService = userStorageService;
        _authRepository = authRepository;
    }

    public async Task HandleEventAsync(string eventType, string email, string? gatoolMergeField,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing Mailchimp webhook: type={EventType}, email={Email}", eventType, email);

        await EnsureClientsInitializedAsync(cancellationToken);
        await EnsureAuth0TokenFreshAsync(cancellationToken);

        switch (eventType)
        {
            case "subscribe":
            case "profile":
                await HandleSubscribeOrProfileAsync(email, gatoolMergeField, cancellationToken);
                break;
            case "unsubscribe":
            case "cleaned":
                await HandleUnsubscribeAsync(email, cancellationToken);
                break;
            default:
                _logger.LogWarning("Unhandled Mailchimp webhook event type: {EventType}", eventType);
                break;
        }

        await _userStorageService.RecordWebhookEvent(eventType, email);
    }

    private async Task HandleSubscribeOrProfileAsync(string email, string? gatoolMergeField,
        CancellationToken cancellationToken)
    {
        var isOptedIn = gatoolMergeField == OptInText;

        // === New auth (DynamoDB) ===
        // "Opted in" gets the full "user" role (read + write).
        // "Not opted in" gets no roles (record exists but no API access — equivalent
        // to the previous Auth0 read-only role, since all gated endpoints require "user").
        var roles = isOptedIn ? new[] { "user" } : Array.Empty<string>();

        var existing = await _authRepository.GetUserAsync(email, cancellationToken);
        var newAuthCreated = existing == null;
        if (newAuthCreated)
        {
            await _authRepository.UpsertUserAsync(email, roles, cancellationToken);
            _logger.LogInformation("Created auth user for {Email} with roles=[{Roles}]",
                email, string.Join(",", roles));
        }
        else
        {
            await _authRepository.SetRolesAsync(email, roles, cancellationToken);
            _logger.LogInformation("Updated roles for {Email} to [{Roles}]", email, string.Join(",", roles));
        }

        // === Legacy auth (Auth0) — mirror until cutover ===
        bool auth0Created = false;
        try
        {
            var (user, created) = await GetOrCreateAuth0UserAsync(email, cancellationToken);
            auth0Created = created;
            if (isOptedIn)
            {
                _logger.LogInformation("Auth0: assigning full user role to {Email}", email);
                await _auth0RateLimiter.AcquireAsync(cancellationToken: cancellationToken);
                await _auth0Client!.Users.AssignRolesAsync(user.UserId, new AssignRolesRequest
                {
                    Roles = [FullUserRoleId]
                }, cancellationToken);
            }
            else
            {
                _logger.LogInformation("Auth0: assigning read-only role to {Email}", email);
                await _auth0RateLimiter.AcquireAsync(cancellationToken: cancellationToken);
                await _auth0Client!.Users.AssignRolesAsync(user.UserId, new AssignRolesRequest
                {
                    Roles = [ReadOnlyRoleId]
                }, cancellationToken);
                await _auth0RateLimiter.AcquireAsync(cancellationToken: cancellationToken);
                await _auth0Client!.Users.RemoveRolesAsync(user.UserId, new AssignRolesRequest
                {
                    Roles = [FullUserRoleId]
                }, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Auth0 mirror failed for {Email} during subscribe/profile — continuing", email);
        }

        // Tag for welcome only the first time we see this user in EITHER system,
        // so resubscribers don't get re-welcomed.
        if (newAuthCreated || auth0Created)
        {
            await TagSubscriberForWelcomeAsync(email, cancellationToken);
        }
    }

    private async Task HandleUnsubscribeAsync(string email, CancellationToken cancellationToken)
    {
        // New auth
        await _authRepository.DeleteUserAsync(email, cancellationToken);
        _logger.LogInformation("Deleted auth account for {Email}", email);

        // Legacy auth (Auth0) — mirror until cutover
        try
        {
            await _auth0RateLimiter.AcquireAsync(cancellationToken: cancellationToken);
            var users = (await _auth0Client!.Users.GetUsersByEmailAsync(email,
                    cancellationToken: cancellationToken))
                .Where(u => u.Identities.Any(i => i.Provider == "email")).ToList();

            foreach (var user in users)
            {
                await _auth0RateLimiter.AcquireAsync(cancellationToken: cancellationToken);
                await _auth0Client.Users.DeleteAsync(user.UserId);
                _logger.LogInformation("Deleted Auth0 account for {Email}", email);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Auth0 mirror failed for {Email} during unsubscribe — continuing", email);
        }
    }

    private async Task<(User user, bool created)> GetOrCreateAuth0UserAsync(string email,
        CancellationToken cancellationToken)
    {
        await _auth0RateLimiter.AcquireAsync(cancellationToken: cancellationToken);
        var existing = (await _auth0Client!.Users.GetUsersByEmailAsync(email.ToLowerInvariant(),
                cancellationToken: cancellationToken))
            .Where(u => u.Identities.Any(i => i.Provider == "email")).ToList();

        if (existing.Count >= 1)
            return (existing.First(), false);

        await _auth0RateLimiter.AcquireAsync(cancellationToken: cancellationToken);
        var newUser = await _auth0Client.Users.CreateAsync(new UserCreateRequest
        {
            Connection = "email", Email = email, EmailVerified = true
        }, cancellationToken);

        return (newUser, true);
    }

    private async Task TagSubscriberForWelcomeAsync(string email, CancellationToken cancellationToken)
    {
        try
        {
            await _mailchimpRateLimiter.AcquireAsync(cancellationToken: cancellationToken);
            using var md5 = System.Security.Cryptography.MD5.Create();
            var subscriberHash = MailChimp.Net.Core.Helper.GetHash(md5, email.ToLowerInvariant());
            await _mailChimpClient!.Members.AddTagsAsync(_mailChimpListId!, subscriberHash,
                new MailChimp.Net.Models.Tags
                {
                    MemberTags = [new MailChimp.Net.Models.Tag { Name = WelcomeTag, Status = "active" }]
                }, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            // Non-critical — log but don't fail the webhook
            _logger.LogWarning(ex, "Failed to add welcome tag for {Email}", email);
        }
    }

    private async Task EnsureClientsInitializedAsync(CancellationToken cancellationToken)
    {
        if (_mailChimpClient != null) return;

        _auth0ClientId = await _secretProvider.GetSecretAsync("Auth0AdminClientId", cancellationToken);
        _auth0ClientSecret = await _secretProvider.GetSecretAsync("Auth0AdminClientSecret", cancellationToken);
        var mailChimpApiKey = await _secretProvider.GetSecretAsync("MailChimpAPIKey", cancellationToken);
        var mailChimpApiUrl = await _secretProvider.GetSecretAsync("MailchimpAPIURL", cancellationToken);
        _mailChimpListId = await _secretProvider.GetSecretAsync("MailchimpListID", cancellationToken);

        _mailChimpClient = new MailChimpManager(new MailChimpOptions
        {
            ApiKey = mailChimpApiKey,
            DataCenter = mailChimpApiUrl
        });
    }

    private async Task EnsureAuth0TokenFreshAsync(CancellationToken cancellationToken)
    {
        if (_auth0Client != null && DateTimeOffset.UtcNow < _auth0TokenExpiresAt - Auth0TokenRefreshSkew)
            return;

        await _auth0TokenLock.WaitAsync(cancellationToken);
        try
        {
            if (_auth0Client != null && DateTimeOffset.UtcNow < _auth0TokenExpiresAt - Auth0TokenRefreshSkew)
                return;

            _logger.LogInformation("Refreshing Auth0 management API token");
            var authClient = new AuthenticationApiClient(Auth0Domain);
            var token = await authClient.GetTokenAsync(new ClientCredentialsTokenRequest
            {
                Audience = $"https://{Auth0Domain}/api/v2/",
                ClientId = _auth0ClientId!,
                ClientSecret = _auth0ClientSecret!
            }, cancellationToken);

            _auth0Client = new ManagementApiClient(token.AccessToken, Auth0Domain);
            _auth0TokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn);
        }
        finally
        {
            _auth0TokenLock.Release();
        }
    }
}
