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
    /// <response code="200">The webhook URL is reachable; no response body or secret is required for this GET.</response>
    [HttpGet("/webhooks/mailchimp")]
    public IActionResult ValidateWebhook()
    {
        return Ok();
    }

    /// <summary>
    ///     Receives Mailchimp webhook events (subscribe, unsubscribe, profile, cleaned).
    ///     Payload is form-encoded, not JSON.
    /// </summary>
    /// <remarks>
    ///     Send the shared webhook secret in the secret query parameter. The application/x-www-form-urlencoded body
    ///     requires type and data[email]; data[merges][GATOOL] is optional.
    ///     Processing failures are logged but acknowledged with HTTP 200 to prevent retry floods.
    /// </remarks>
    /// <param name="cancellationToken">Request cancellation token; not a client-supplied parameter.</param>
    /// <response code="200">The event was acknowledged, including logged processing failures.</response>
    /// <response code="400">The event type or email is missing.</response>
    /// <response code="401">The webhook secret is missing or invalid.</response>
    [HttpPost("/webhooks/mailchimp")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
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