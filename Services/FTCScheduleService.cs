using GAToolAPI.Models;

namespace GAToolAPI.Services;

// ReSharper disable once InconsistentNaming
public class FTCScheduleService(FTCApiService ftcApiClient)
{
    public async Task<HybridSchedule?> BuildHybridSchedule(string year, string eventCode, string tournamentLevel)
    {
        // FTC API has a built-in hybrid endpoint that merges schedule + results
        var response = await ftcApiClient.Get<FTCHybridScheduleResponse>(
            $"{year}/schedule/{eventCode}/{tournamentLevel}/hybrid");

        if (response?.Schedule == null || response.Schedule.Count == 0)
            return null;

        return new HybridSchedule
        {
            Schedule = ConvertToHybridMatches(response.Schedule)
        };
    }

    private static List<HybridMatch> ConvertToHybridMatches(List<FTCHybridMatch> ftcMatches)
    {
        return ftcMatches.Select(m => new HybridMatch
        {
            Field = m.Field,
            StartTime = m.StartTime,
            ActualStartTime = m.ActualStartTime,
            MatchVideoLink = m.MatchVideoLink,
            MatchNumber = m.MatchNumber,
            IsReplay = m.IsReplay,
            TournamentLevel = m.TournamentLevel,
            PostResultTime = m.PostResultTime,
            Description = m.Description,
            ScoreRedFinal = m.ScoreRedFinal,
            ScoreRedFoul = m.ScoreRedFoul,
            ScoreRedAuto = m.ScoreRedAuto,
            ScoreBlueFinal = m.ScoreBlueFinal,
            ScoreBlueFoul = m.ScoreBlueFoul,
            ScoreBlueAuto = m.ScoreBlueAuto,
            Teams = m.Teams?.Select(t => new HybridTeam
            {
                TeamNumber = t.TeamNumber ?? 0,
                Station = t.Station ?? string.Empty,
                Surrogate = t.Surrogate,
                Dq = t.Dq is true or "true"
            }).ToList() ?? []
        }).ToList();
    }
}
