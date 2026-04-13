using GAToolAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NSwag.Annotations;

namespace GAToolAPI.Controllers;

[OpenApiTag("Webhooks")]
[AllowAnonymous]
public class WebhookController(
    MailchimpWebhookService webhookService,
    ISecretProvider secretProvider,
    ILogger<WebhookController> logger) : ControllerBase
{
    private string? _webhookSecret;

    /// <summary>
    ///     Mailchimp webhook URL validation. Mailchimp sends a GET request to verify the URL on registration.
    /// </summary>
    [HttpGet("/webhooks/mailchimp")]
    public IActionResult ValidateWebhook()
    {
        return Ok();
    }

    /// <summary>
    ///     Receives Mailchimp webhook events (subscribe, unsubscribe, profile, cleaned).
    ///     Payload is form-encoded, not JSON.
    /// </summary>
    [HttpPost("/webhooks/mailchimp")]
    public async Task<IActionResult> HandleWebhook(CancellationToken cancellationToken)
    {
        _webhookSecret ??= secretProvider.GetSecret("MailchimpWebhookSecret");
        var querySecret = Request.Query["secret"].FirstOrDefault();

        if (string.IsNullOrEmpty(querySecret) || querySecret != _webhookSecret)
        {
            logger.LogWarning("Mailchimp webhook received with invalid or missing secret");
            return Unauthorized();
        }

        var form = await Request.ReadFormAsync(cancellationToken);

        var eventType = form["type"].FirstOrDefault();
        var email = form["data[email]"].FirstOrDefault();
        var gatoolMergeField = form["data[merges][GATOOL]"].FirstOrDefault();

        if (string.IsNullOrEmpty(eventType) || string.IsNullOrEmpty(email))
        {
            logger.LogWarning("Mailchimp webhook missing type or email: type={Type}, email={Email}",
                eventType, email);
            return BadRequest("Missing required fields");
        }

        try
        {
            await webhookService.HandleEventAsync(eventType, email, gatoolMergeField, cancellationToken);
            return Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing Mailchimp webhook: type={Type}, email={Email}",
                eventType, email);
            // Return 200 anyway to prevent Mailchimp from retrying and flooding us
            return Ok();
        }
    }
}
