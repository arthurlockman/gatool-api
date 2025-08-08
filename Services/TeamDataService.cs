using System.Text.Json.Nodes;
using GAToolAPI.Extensions;

namespace GAToolAPI.Services;

public class TeamDataService(FRCApiService frcApiClient, FTCApiService ftcApiClient, ILogger<TeamDataService> logger)
{
    public async Task<JsonObject?> GetFrcTeamData(string year, string? eventCode = null, string? districtCode = null,
        string? teamNumber = null)
    {
        var parameters = new { eventCode, districtCode, teamNumber }.ToParameterDictionary();
        return await DepaginateTeamData($"{year}/teams", frcApiClient, parameters);
    }

    public async Task<JsonObject?> GetFtcTeamData(string year, string? eventCode = null, int? teamNumber = null,
        string? state = null)
    {
        var parameters = new { eventCode, state, teamNumber }.ToParameterDictionary();
        try
        {
            return await DepaginateTeamData($"{year}/teams", ftcApiClient, parameters);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.Message);
            return null;
        }
    }

    private static async Task<JsonObject?> DepaginateTeamData(string path, IApiService client,
        Dictionary<string, string?> query)
    {
        var result = await client.GetGeneric(path, query);
        if (result == null) return null;

        var pageTotal = result["pageTotal"]?.GetValue<int>() ?? 1;
        if (pageTotal == 1) return result;

        var allTeams = new List<JsonNode?>();
        var teams = result["teams"]?.AsArray();
        if (teams != null) allTeams.AddRange(teams);

        var pageTasks = new List<Task<JsonArray?>>();
        for (var page = 2; page <= pageTotal; page++)
        {
            var pageParameters = new Dictionary<string, string?>(query)
            {
                ["page"] = page.ToString()
            };

            pageTasks.Add(Task.Run(async () =>
            {
                var pageResult = await client.GetGeneric(path, pageParameters);
                return pageResult?["teams"]?.AsArray();
            }));
        }

        var pageResults = await Task.WhenAll(pageTasks);
        allTeams.AddRange(pageResults.Where(x => x != null).SelectMany(x => x?.AsArray() ?? []));

        var newTeamsArray = new JsonArray();
        foreach (var team in allTeams.OfType<JsonNode>()) newTeamsArray.Add(team.DeepClone());

        result["teams"] = newTeamsArray;
        result["teamCountPage"] = allTeams.Count;
        result["teamCountTotal"] = allTeams.Count;
        result["pageCurrent"] = 1;
        result["pageTotal"] = 1;

        return result;
    }
}