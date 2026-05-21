using System.Threading.RateLimiting;
using GAToolAPI.Services.Auth;
using MailChimp.Net;
using MailChimp.Net.Core;

namespace GAToolAPI.Services;

/// <summary>
/// Handles Mailchimp subscribe/unsubscribe/profile webhooks and mirrors the
/// resulting role state into the gatool auth user store (DynamoDB).
/// </summary>
public class MailchimpWebhookService
{
    private const string OptInText =
        "I want access to gatool and agree that I will not abuse this access to team data.";

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

    private MailChimpManager? _mailChimpClient;
    private string? _mailChimpListId;

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

        // "Opted in" gets the full "user" role (read + write).
        // "Not opted in" gets no roles (record exists but no API access — all
        // gated endpoints require "user").
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

        // Tag for welcome only the first time we see this user, so resubscribers
        // don't get re-welcomed.
        if (newAuthCreated)
        {
            await TagSubscriberForWelcomeAsync(email, cancellationToken);
        }
    }

    private async Task HandleUnsubscribeAsync(string email, CancellationToken cancellationToken)
    {
        await _authRepository.DeleteUserAsync(email, cancellationToken);
        _logger.LogInformation("Deleted auth account for {Email}", email);
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

        var mailChimpApiKey = await _secretProvider.GetSecretAsync("MailChimpAPIKey", cancellationToken);
        var mailChimpApiUrl = await _secretProvider.GetSecretAsync("MailchimpAPIURL", cancellationToken);
        _mailChimpListId = await _secretProvider.GetSecretAsync("MailchimpListID", cancellationToken);

        _mailChimpClient = new MailChimpManager(new MailChimpOptions
        {
            ApiKey = mailChimpApiKey,
            DataCenter = mailChimpApiUrl
        });
    }
}
