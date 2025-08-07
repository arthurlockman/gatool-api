using GAToolAPI.Models;
using GAToolAPI.Services;

namespace GAToolAPI.Extensions;

public static class ScoreExtensions
{
    public static async Task StoreHighScores(this IEnumerable<HighScore> highScores, UserStorageService storage,
        int year,
        string? typePrefix = null)
    {
        foreach (var highScore in highScores) await storage.StoreHighScore(year, highScore, typePrefix);
    }

    public static List<HighScore> CalculateHighScores(this IEnumerable<HybridMatch> matches, int year,
        string? prefix = null)
    {
        var matchList = matches.ToList();

        // Initialize buckets for different score categories
        var overallHighScorePlayoff = new List<HybridMatch>();
        var overallHighScoreQual = new List<HybridMatch>();
        var penaltyFreeHighScorePlayoff = new List<HybridMatch>();
        var penaltyFreeHighScoreQual = new List<HybridMatch>();
        var tbaPenaltyFreeHighScorePlayoff = new List<HybridMatch>();
        var tbaPenaltyFreeHighScoreQual = new List<HybridMatch>();
        var offsettingPenaltyHighScorePlayoff = new List<HybridMatch>();
        var offsettingPenaltyHighScoreQual = new List<HybridMatch>();

        // Categorize matches based on tournament level and penalty conditions
        foreach (var match in matchList)
        {
            var isPlayoff = match.TournamentLevel?.ToLower() == "playoff";
            var isQual = match.TournamentLevel?.ToLower() == "qualification";

            var scoreBlueFoul = match.ScoreBlueFoul ?? 0;
            var scoreRedFoul = match.ScoreRedFoul ?? 0;
            var scoreBlueFinal = match.ScoreBlueFinal ?? 0;
            var scoreRedFinal = match.ScoreRedFinal ?? 0;

            // Overall scores (all matches)
            if (isPlayoff)
                overallHighScorePlayoff.Add(match);
            if (isQual)
                overallHighScoreQual.Add(match);

            // TBA penalty-free (winning alliance had no fouls)
            if (isPlayoff &&
                ((scoreBlueFoul == 0 && scoreBlueFinal >= scoreRedFinal) ||
                 (scoreRedFoul == 0 && scoreBlueFinal <= scoreRedFinal)))
                tbaPenaltyFreeHighScorePlayoff.Add(match);

            if (isQual &&
                ((scoreBlueFoul == 0 && scoreBlueFinal >= scoreRedFinal) ||
                 (scoreRedFoul == 0 && scoreBlueFinal <= scoreRedFinal)))
                tbaPenaltyFreeHighScoreQual.Add(match);

            // Penalty-free matches (no fouls on either alliance)
            if (isPlayoff && scoreBlueFoul == 0 && scoreRedFoul == 0)
                penaltyFreeHighScorePlayoff.Add(match);
            else if (isQual && scoreBlueFoul == 0 && scoreRedFoul == 0)
                penaltyFreeHighScoreQual.Add(match);
            // Offsetting penalties (equal fouls on both alliances, both > 0)
            else if (isPlayoff && scoreBlueFoul == scoreRedFoul && scoreBlueFoul > 0)
                offsettingPenaltyHighScorePlayoff.Add(match);
            else if (isQual && scoreBlueFoul == scoreRedFoul && scoreBlueFoul > 0)
                offsettingPenaltyHighScoreQual.Add(match);
        }

        // Build high score results
        var highScoresData = new List<HighScore>();

        if (overallHighScorePlayoff.Count > 0)
        {
            var (match, alliance, score) = FindHighestScore(overallHighScorePlayoff);
            highScoresData.Add(BuildHighScoreJson(year, "overall", "playoff", match, alliance, score, prefix));
        }

        if (overallHighScoreQual.Count > 0)
        {
            var (match, alliance, score) = FindHighestScore(overallHighScoreQual);
            highScoresData.Add(BuildHighScoreJson(year, "overall", "qual", match, alliance, score, prefix));
        }

        if (penaltyFreeHighScorePlayoff.Count > 0)
        {
            var (match, alliance, score) = FindHighestScore(penaltyFreeHighScorePlayoff);
            highScoresData.Add(BuildHighScoreJson(year, "penaltyFree", "playoff", match, alliance, score, prefix));
        }

        if (penaltyFreeHighScoreQual.Count > 0)
        {
            var (match, alliance, score) = FindHighestScore(penaltyFreeHighScoreQual);
            highScoresData.Add(BuildHighScoreJson(year, "penaltyFree", "qual", match, alliance, score, prefix));
        }

        if (offsettingPenaltyHighScorePlayoff.Count > 0)
        {
            var (match, alliance, score) = FindHighestScore(offsettingPenaltyHighScorePlayoff);
            highScoresData.Add(BuildHighScoreJson(year, "offsetting", "playoff", match, alliance, score, prefix));
        }

        if (offsettingPenaltyHighScoreQual.Count > 0)
        {
            var (match, alliance, score) = FindHighestScore(offsettingPenaltyHighScoreQual);
            highScoresData.Add(BuildHighScoreJson(year, "offsetting", "qual", match, alliance, score, prefix));
        }

        if (tbaPenaltyFreeHighScoreQual.Count > 0)
        {
            var (match, alliance, score) = FindHighestScore(tbaPenaltyFreeHighScoreQual);
            highScoresData.Add(BuildHighScoreJson(year, "TBAPenaltyFree", "qual", match, alliance, score, prefix));
        }

        if (tbaPenaltyFreeHighScorePlayoff.Count > 0)
        {
            var (match, alliance, score) = FindHighestScore(tbaPenaltyFreeHighScorePlayoff);
            highScoresData.Add(BuildHighScoreJson(year, "TBAPenaltyFree", "playoff", match, alliance, score, prefix));
        }

        return highScoresData;
    }

    private static (HybridMatch match, string alliance, int score) FindHighestScore(IEnumerable<HybridMatch> matches)
    {
        var matchList = matches.ToList();
        if (matchList.Count == 0) return (new HybridMatch(), "", 0);

        // Find the match with the highest score from either alliance
        var bestMatch = matchList
            .Select(m => new
            {
                Match = m,
                BlueScore = m.ScoreBlueFinal ?? 0,
                RedScore = m.ScoreRedFinal ?? 0,
                HighestScore = Math.Max(m.ScoreBlueFinal ?? 0, m.ScoreRedFinal ?? 0),
                WinningAlliance = (m.ScoreBlueFinal ?? 0) >= (m.ScoreRedFinal ?? 0) ? "blue" : "red"
            })
            .OrderByDescending(x => x.HighestScore)
            .First();

        return (bestMatch.Match, bestMatch.WinningAlliance, bestMatch.HighestScore);
    }

    private static HighScore BuildHighScoreJson(int year, string category, string tournamentLevel,
        HybridMatch match, string alliance, int score, string? prefix)
    {
        return new HighScore
        {
            Level = tournamentLevel,
            MatchData = new MatchData
            {
                Event = new EventInfo
                {
                    DistrictCode = match.DistrictCode,
                    EventCode = match.EventCode,
                    Type = tournamentLevel
                },
                HighScoreAlliance = alliance,
                Match = match
            },
            Type = category,
            Year = year,
            YearType = $"{year}{prefix}{category}{tournamentLevel}"
        };
    }
}