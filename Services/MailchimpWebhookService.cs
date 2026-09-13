using System.Threading.RateLimiting;
using GAToolAPI.AuthExtensions;
using GAToolAPI.Services.Auth;
using MailChimp.Net;

namespace GAToolAPI.Services;

/// <summary>
/// Handles Mailchimp subscribe/unsubscribe/profile webhooks and mirrors the
/// resulting role state into the gatool auth user store (DynamoDB).
/// </summary>
public class MailchimpWebhookService(
    ILogger<MailchimpWebhookService> logger,
    ISecretProvider secretProvider,
    UserStorageService userStorageService,
    AuthRepository authRepository)
{
    private const string OptInText =
        "I want access to gatool and agree that I will not abuse this access to team data.";

    private const string WelcomeTag = "gatool-welcome";

    private readonly SlidingWindowRateLimiter _mailchimpRateLimiter = new(new SlidingWindowRateLimiterOptions
    {
        AutoReplenishment = true,
        Window = TimeSpan.FromSeconds(1),
        PermitLimit = 5,
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        SegmentsPerWindow = 1,
        QueueLimit = int.MaxValue
    });

    private MailChimpManager? _mailChimpClient;
    private string? _mailChimpListId;

    public async Task HandleEventAsync(string eventType, string email, string? gatoolMergeField,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Processing Mailchimp webhook: type={EventType}, email={Email}", eventType, email);

        await EnsureClientsInitializedAsync(cancellationToken);

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
                logger.LogWarning("Unhandled Mailchimp webhook event type: {EventType}", eventType);
                break;
        }

        await userStorageService.RecordWebhookEvent(eventType, email);
    }

    private async Task HandleSubscribeOrProfileAsync(string email, string? gatoolMergeField,
        CancellationToken cancellationToken)
    {
        var isOptedIn = gatoolMergeField == OptInText;

        // Mailchimp owns only the user role. Admin and manually assigned roles
        // are preserved when a subscriber's opt-in state changes.
        var existing = await authRepository.GetUserAsync(email, cancellationToken);
        var newAuthCreated = existing == null;
        var user = newAuthCreated
            ? await authRepository.UpsertUserAsync(
                email, isOptedIn ? [AuthRoles.User] : [], cancellationToken)
            : await authRepository.SetRolePresenceAsync(
                email, AuthRoles.User, isOptedIn, cancellationToken);
        logger.LogInformation("Updated Mailchimp-managed user role for {Email}; roles=[{Roles}]",
            email, string.Join(",", user?.Roles ?? []));

        // Tag for welcome only the first time we see this user, so resubscribers
        // don't get re-welcomed.
        if (newAuthCreated) await TagSubscriberForWelcomeAsync(email, cancellationToken);
    }

    private async Task HandleUnsubscribeAsync(string email, CancellationToken cancellationToken)
    {
        await authRepository.DeleteUserAsync(email, cancellationToken);
        logger.LogInformation("Deleted auth account for {Email}", email);
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
            logger.LogWarning(ex, "Failed to add welcome tag for {Email}", email);
        }
    }

    private async Task EnsureClientsInitializedAsync(CancellationToken cancellationToken)
    {
        if (_mailChimpClient != null) return;

        var mailChimpApiKey = await secretProvider.GetSecretAsync("MailChimpAPIKey", cancellationToken);
        var mailChimpApiUrl = await secretProvider.GetSecretAsync("MailchimpAPIURL", cancellationToken);
        _mailChimpListId = await secretProvider.GetSecretAsync("MailchimpListID", cancellationToken);

        _mailChimpClient = new MailChimpManager(new MailChimpOptions
        {
            ApiKey = mailChimpApiKey,
            DataCenter = mailChimpApiUrl
        });
    }
}