using GAToolAPI.Models;

namespace GAToolAPI.Services;

public class ScheduleService(FRCApiService frcApiClient)
{
    public async Task<HybridSchedule?> BuildHybridSchedule(string year, string eventCode, string tournamentLevel)
    {
        // Fetch schedule data using typed method
        var scheduleResponse =
            await frcApiClient.Get<ScheduleResponse>($"{year}/schedule/{eventCode}/{tournamentLevel}");
        if (scheduleResponse?.Schedule == null) return null;

        // Try to fetch matches data
        MatchResponse? matchesResponse;
        try
        {
            matchesResponse = await frcApiClient.Get<MatchResponse>($"{year}/matches/{eventCode}/{tournamentLevel}");
        }
        catch
        {
            // If matches request fails, return just the schedule data as HybridMatches
            return new HybridSchedule
            {
                Schedule = ConvertScheduleToHybrid(scheduleResponse.Schedule)
            };
        }

        if (matchesResponse?.Matches == null)
            return new HybridSchedule
            {
                Schedule = ConvertScheduleToHybrid(scheduleResponse.Schedule)
            };

        // Merge schedule and matches data
        var hybridMatches = MergeScheduleAndMatchesTyped(scheduleResponse.Schedule, matchesResponse.Matches);

        return new HybridSchedule
        {
            Schedule = hybridMatches
        };
    }

    private static List<HybridMatch> ConvertScheduleToHybrid(List<ScheduleMatch> scheduleMatches)
    {
        return scheduleMatches.Select(s => new HybridMatch
        {
            Field = s.Field,
            StartTime = s.StartTime,
            MatchNumber = s.MatchNumber,
            TournamentLevel = s.TournamentLevel,
            Description = s.Description,
            Teams = s.Teams?.Select(t => new HybridTeam
            {
                TeamNumber = t.TeamNumber,
                Station = t.Station ?? string.Empty,
                Surrogate = t.Surrogate,
                Dq = false // Default for schedule-only data
            }).ToList() ?? []
        }).ToList();
    }

    private static List<HybridMatch> MergeScheduleAndMatchesTyped(List<ScheduleMatch> schedule, List<Match> matches)
    {
        // Create lookup dictionary for matches by match number
        var matchLookup = matches.ToDictionary(m => m.MatchNumber, m => m);

        return schedule.Select(scheduleMatch =>
        {
            var hybridMatch = new HybridMatch
            {
                Field = scheduleMatch.Field,
                StartTime = scheduleMatch.StartTime,
                MatchNumber = scheduleMatch.MatchNumber,
                TournamentLevel = scheduleMatch.TournamentLevel,
                Description = scheduleMatch.Description,
                Teams = scheduleMatch.Teams?.Select(t => new HybridTeam
                {
                    TeamNumber = t.TeamNumber,
                    Station = t.Station ?? string.Empty,
                    Surrogate = t.Surrogate,
                    Dq = false // Default, will be overwritten if match data exists
                }).ToList() ?? []
            };

            // Merge match data if it exists
            if (matchLookup.TryGetValue(scheduleMatch.MatchNumber, out var matchData))
            {
                hybridMatch.ActualStartTime = matchData.ActualStartTime;
                hybridMatch.PostResultTime = matchData.PostResultTime;
                hybridMatch.ScoreRedFinal = matchData.ScoreRedFinal;
                hybridMatch.ScoreRedFoul = matchData.ScoreRedFoul;
                hybridMatch.ScoreRedAuto = matchData.ScoreRedAuto;
                hybridMatch.ScoreBlueFinal = matchData.ScoreBlueFinal;
                hybridMatch.ScoreBlueFoul = matchData.ScoreBlueFoul;
                hybridMatch.ScoreBlueAuto = matchData.ScoreBlueAuto;
                hybridMatch.AutoStartTime = matchData.AutoStartTime;
                hybridMatch.MatchVideoLink = matchData.MatchVideoLink;
                hybridMatch.IsReplay = matchData.IsReplay;

                // Merge team data (DQ status from match results)
                var matchTeamLookup = matchData.Teams?.ToDictionary(t => t.Station, t => t);
                foreach (var hybridTeam in hybridMatch.Teams)
                    if (matchTeamLookup != null && matchTeamLookup.TryGetValue(hybridTeam.Station, out var matchTeam))
                        hybridTeam.Dq = matchTeam.Dq;
            }

            return hybridMatch;
        }).ToList();
    }
}