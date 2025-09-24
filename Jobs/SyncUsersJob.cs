using System.Threading.RateLimiting;
using Auth0.AuthenticationApi;
using Auth0.AuthenticationApi.Models;
using Auth0.ManagementApi;
using Auth0.ManagementApi.Models;
using Azure.Security.KeyVault.Secrets;
using GAToolAPI.Services;
using MailChimp.Net;
using MailChimp.Net.Core;
using MailChimp.Net.Models;
using NewRelic.Api.Agent;

namespace GAToolAPI.Jobs;

public class SyncUsersJob(
    ILogger<SyncUsersJob> logger,
    UserStorageService userStorageService,
    SecretClient secretClient)
    : IJob
{
    private const string OptInText =
        "I want access to gatool and agree that I will not abuse this access to team data.";

    private const string MergeFieldName = "GATOOL";
    private const string Auth0Domain = "gatool.auth0.com";
    private const string FullUserRoleId = "rol_KRLODHx3eNItUgvI";
    private const string ReadOnlyRoleId = "rol_EQcREtmOWaGanRYG";

    public string Name => "SyncUsers";

    [Transaction]
    [Trace]
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting SyncUsers job...");

        try
        {
            // Retrieve secrets asynchronously during execution instead of constructor
            var mailChimpApiKey =
                (await secretClient.GetSecretAsync("MailChimpAPIKey", cancellationToken: cancellationToken)).Value
                .Value;
            var mailChimpApiUrl =
                (await secretClient.GetSecretAsync("MailchimpAPIURL", cancellationToken: cancellationToken)).Value
                .Value;
            var mailChimpListId =
                (await secretClient.GetSecretAsync("MailchimpListID", cancellationToken: cancellationToken)).Value
                .Value;
            var auth0ClientId =
                (await secretClient.GetSecretAsync("Auth0AdminClientId", cancellationToken: cancellationToken)).Value
                .Value;
            var auth0ClientSecret =
                (await secretClient.GetSecretAsync("Auth0AdminClientSecret", cancellationToken: cancellationToken))
                .Value.Value;

            var authClient = new AuthenticationApiClient(Auth0Domain);

            // Fetch the access token using the Client Credentials.
            var accessTokenResponse = await authClient.GetTokenAsync(new ClientCredentialsTokenRequest
            {
                Audience = $"https://{Auth0Domain}/api/v2/",
                ClientId = auth0ClientId,
                ClientSecret = auth0ClientSecret
            }, cancellationToken);

            var auth0ManagementApiClient = new ManagementApiClient(accessTokenResponse.AccessToken, Auth0Domain);
            var auth0ApiRateLimiter = new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                Window = TimeSpan.FromSeconds(1),
                PermitLimit = 2, // auth0 allows 15RPS but in practice 15 overwhelms it
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                SegmentsPerWindow = 1,
                QueueLimit = int.MaxValue
            });

            var mailChimpClient = new MailChimpManager(new MailChimpOptions
            {
                ApiKey = mailChimpApiKey,
                DataCenter = mailChimpApiUrl
            });

            // Get all members from the MailChimp list
            var members =
                (await mailChimpClient.Members.GetAllAsync(mailChimpListId, cancellationToken: cancellationToken))
                .ToList();

            var optedInUsers = await Task.WhenAll(members.Where(m =>
                    m.Status == Status.Subscribed && m.MergeFields.TryGetValue(MergeFieldName, out var value) &&
                    (string)value == OptInText)
                .Select(async m =>
                    await GetOrCreateUser(m.EmailAddress, auth0ManagementApiClient, auth0ApiRateLimiter,
                        cancellationToken)));
            logger.LogInformation("There are {Count} opted-in-users.", optedInUsers.Length);
            foreach (var user in optedInUsers)
            {
                await auth0ApiRateLimiter.AcquireAsync(cancellationToken: cancellationToken);
                logger.LogInformation("Assigning full user role to {UserEmail}", user.user.Email);
                await auth0ManagementApiClient.Users.AssignRolesAsync(user.user.UserId, new AssignRolesRequest
                {
                    Roles = [FullUserRoleId]
                }, cancellationToken);
            }

            var optedOutUsers = await Task.WhenAll(members.Where(m =>
                    m.Status == Status.Subscribed && m.MergeFields.TryGetValue(MergeFieldName, out var value) &&
                    (string)value != OptInText)
                .Select(async m =>
                    await GetOrCreateUser(m.EmailAddress, auth0ManagementApiClient, auth0ApiRateLimiter,
                        cancellationToken)));
            logger.LogInformation("There are {Count} opted-out-users.", optedOutUsers.Length);
            foreach (var user in optedOutUsers)
            {
                await auth0ApiRateLimiter.AcquireAsync(cancellationToken: cancellationToken);
                logger.LogInformation("Assigning read-only user role to {UserEmail}", user.user.Email);
                await auth0ManagementApiClient.Users.AssignRolesAsync(user.user.UserId, new AssignRolesRequest
                {
                    Roles = [ReadOnlyRoleId]
                }, cancellationToken);
                await auth0ApiRateLimiter.AcquireAsync(cancellationToken: cancellationToken);
                await auth0ManagementApiClient.Users.RemoveRolesAsync(user.user.UserId, new AssignRolesRequest
                {
                    Roles = [FullUserRoleId]
                }, cancellationToken);
            }

            var unsubscribedUsers = members.Where(m => m.Status == Status.Unsubscribed)
                .Select(m => m.EmailAddress).ToList();
            logger.LogInformation("There are {Count} Unsubscribed users. Deleting accounts.", unsubscribedUsers.Count);
            var deletedUsers = 0;
            foreach (var email in unsubscribedUsers)
            {
                await auth0ApiRateLimiter.AcquireAsync(cancellationToken: cancellationToken);
                var emailUser =
                    (await auth0ManagementApiClient.Users.GetUsersByEmailAsync(email,
                        cancellationToken: cancellationToken))
                    .Where(u => u.Identities.Any(i => i.Provider == "email")).ToList();

                if (emailUser.Count <= 0) continue;

                await auth0ApiRateLimiter.AcquireAsync(cancellationToken: cancellationToken);
                await auth0ManagementApiClient.Users.DeleteAsync(emailUser.First().UserId);
                deletedUsers++;
                logger.LogInformation("Deleted email user {Email}", email);
            }

            if (optedInUsers.Any(u => u.created) || optedOutUsers.Any(u => u.created))
            {
                logger.LogInformation("Added users, sending new welcome campaign...");

                var campaigns = await mailChimpClient.Campaigns.GetAllAsync(new CampaignRequest
                {
                    Status = CampaignStatus.Sent,
                    SortField = CampaignSortField.SendTime,
                    SortOrder = CampaignSortOrder.DESC
                }, cancellationToken);

                var mostRecentSend = campaigns
                    .FirstOrDefault(c => c.Settings.SubjectLine.Contains("Welcome to the FIRST gatool!"));

                if (mostRecentSend != null)
                {
                    logger.LogInformation("Copying campaign {CampaignId}", mostRecentSend.Id);
                    var newCampaign =
                        await mailChimpClient.Campaigns.ReplicateCampaignAsync(mostRecentSend.Id,
                            cancellationToken);
                    logger.LogInformation("Sending campaign {CampaignId}", newCampaign.Id);
                    await mailChimpClient.Campaigns.SendAsync(newCampaign.Id, cancellationToken);
                }
                else
                {
                    logger.LogWarning(
                        "No welcome campaign found with subject line containing 'Welcome to the FIRST gatool!'");
                }
            }
            else
            {
                logger.LogInformation("Did not add any new users, no welcome campaign sent.");
            }

            await userStorageService.SaveUserSyncResults(optedOutUsers.Length, optedOutUsers.Length, deletedUsers);
            logger.LogInformation("SyncUsers job completed successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing SyncUsers job");
            throw;
        }
    }

    private static async Task<(User user, bool created)> GetOrCreateUser(string email,
        ManagementApiClient managementApiClient,
        SlidingWindowRateLimiter rateLimiter, CancellationToken cancellationToken)
    {
        await rateLimiter.AcquireAsync(cancellationToken: cancellationToken);
        var emailUser =
            (await managementApiClient.Users.GetUsersByEmailAsync(email.ToLowerInvariant(), cancellationToken: cancellationToken))
            .Where(u => u.Identities.Any(i => i.Provider == "email")).ToList();
        switch (emailUser.Count)
        {
            case 1:
                return (emailUser.First(), false);
            case 0:
                // User doesn't exist, create it and return
                await rateLimiter.AcquireAsync(cancellationToken: cancellationToken);
                return (await managementApiClient.Users.CreateAsync(new UserCreateRequest
                {
                    Connection = "email", Email = email, EmailVerified = true
                }, cancellationToken), true);
            default:
                throw new Exception($"There are multiple users with email: {email}, intervention needed");
        }
    }
}