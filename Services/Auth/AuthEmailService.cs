using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;

namespace GAToolAPI.Services.Auth;

/// <summary>
///     Sends transactional auth emails (one-time login codes) via Amazon SES v2.
///     The sending domain (gatool.org / auth.gatool.org) is already verified in SES.
/// </summary>
public class AuthEmailService
{
    private const string FromAddress = "gatool <noreply@auth.gatool.org>";
    private const string Subject = "Your gatool login code";

    private readonly IAmazonSimpleEmailServiceV2 _ses;
    private readonly ILogger<AuthEmailService> _logger;

    public AuthEmailService(IAmazonSimpleEmailServiceV2 ses, ILogger<AuthEmailService> logger)
    {
        _ses = ses;
        _logger = logger;
    }

    public async Task SendOtpAsync(string toEmail, string code, TimeSpan validFor,
        CancellationToken ct = default)
    {
        var minutes = (int)Math.Round(validFor.TotalMinutes);

        var text = $"""
            Your gatool login code is: {code}

            This code is valid for {minutes} minutes. If you did not request this code,
            you can safely ignore this email.

            — gatool
            """;

        var html = $"""
            <!doctype html>
            <html><body style="font-family: -apple-system, system-ui, Helvetica, Arial, sans-serif; padding: 24px; color: #222;">
              <h2 style="margin:0 0 12px 0;">Your gatool login code</h2>
              <p>Use this code to finish signing in:</p>
              <p style="font-size: 32px; font-weight: bold; letter-spacing: 6px; margin: 16px 0; padding: 12px 16px; background:#f5f5f5; display:inline-block; border-radius: 6px;">{code}</p>
              <p style="color:#666;">This code is valid for {minutes} minutes. If you didn't request it, you can ignore this message.</p>
              <p style="color:#888; font-size: 12px; margin-top: 32px;">— gatool</p>
            </body></html>
            """;

        try
        {
            await _ses.SendEmailAsync(new SendEmailRequest
            {
                FromEmailAddress = FromAddress,
                Destination = new Destination { ToAddresses = [toEmail] },
                Content = new EmailContent
                {
                    Simple = new Message
                    {
                        Subject = new Content { Data = Subject, Charset = "UTF-8" },
                        Body = new Body
                        {
                            Text = new Content { Data = text, Charset = "UTF-8" },
                            Html = new Content { Data = html, Charset = "UTF-8" }
                        }
                    }
                }
            }, ct);
            _logger.LogInformation("Sent OTP email to {Email}", toEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send OTP email to {Email}", toEmail);
            throw;
        }
    }
}
