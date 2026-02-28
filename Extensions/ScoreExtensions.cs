using GAToolAPI.Models;
using GAToolAPI.Services;

namespace GAToolAPI.Extensions;

public static class ScoreExtensions
{
    public static async Task StoreHighScores(this IEnumerable<HighScore> highScores, UserStorageService storage,
        int year,
        string? typePrefix = null)
    {
        await Task.WhenAll(highScores.Select(highScore => storage.StoreHighScore(year, highScore, typePrefix)));
    }

    public static List<HighScore> CalculateHighScores(this IEnumerable<HybridMatch> matches, int year,
        string? prefix = null)
    {
        var matchList = matches.ToList();
        var isFtc = prefix?.StartsWith("FTC", StringComparison.OrdinalIgnoreCase) == true;

        // Initialize buckets for different score categories
        var overallHighScorePlayoff = new List<HybridMatch>();
        var overallHighScoreQual = new List<HybridMatch>();
        var penaltyFreeHighScorePlayoff = new List<HybridMatch>();
        var penaltyFreeHighScoreQual = new List<HybridMatch>();
        var tbaPenaltyFreeHighScorePlayoff = new List<HybridMatch>();
        var tbaPenaltyFreeHighScoreQual = new List<HybridMatch>();
        var offsettingPenaltyHighScorePlayoff = new List<HybridMatch>();
        var offsettingPenaltyHighScoreQual = new List<HybridMatch>();
        var allianceContributionHighScorePlayoff = new List<HybridMatch>();
        var allianceContributionHighScoreQual = new List<HybridMatch>();

        // Categorize matches based on tournament level and penalty conditions
        foreach (var match in matchList)
        {
            var isPlayoff = match.TournamentLevel?.ToLowerInvariant() == "playoff";
            var isQual = match.TournamentLevel?.ToLowerInvariant() == "qualification";

            var scoreBlueFoul = match.ScoreBlueFoul ?? 0;
            var scoreRedFoul = match.ScoreRedFoul ?? 0;
            var scoreBlueFinal = match.ScoreBlueFinal ?? 0;
            var scoreRedFinal = match.ScoreRedFinal ?? 0;

            // Overall scores (all matches)
            if (isPlayoff)
                overallHighScorePlayoff.Add(match);
            if (isQual)
                overallHighScoreQual.Add(match);

            // Alliance contribution (all matches, scored with penalties deducted)
            if (isPlayoff)
                allianceContributionHighScorePlayoff.Add(match);
            if (isQual)
                allianceContributionHighScoreQual.Add(match);

            // TBA penalty-free (winning alliance had no foul points).
            // FRC: score*Foul = points that alliance receives from opponent fouls → check winner's own foul score.
            // FTC: score*Foul = points that alliance gives to opponent → check opponent's foul score for "winner received none".
            var tbaPenaltyFreePlayoff = isFtc
                ? ((scoreRedFoul == 0 && scoreBlueFinal >= scoreRedFinal) || (scoreBlueFoul == 0 && scoreBlueFinal <= scoreRedFinal))
                : ((scoreBlueFoul == 0 && scoreBlueFinal >= scoreRedFinal) || (scoreRedFoul == 0 && scoreBlueFinal <= scoreRedFinal));
            var tbaPenaltyFreeQual = isFtc
                ? ((scoreRedFoul == 0 && scoreBlueFinal >= scoreRedFinal) || (scoreBlueFoul == 0 && scoreBlueFinal <= scoreRedFinal))
                : ((scoreBlueFoul == 0 && scoreBlueFinal >= scoreRedFinal) || (scoreRedFoul == 0 && scoreBlueFinal <= scoreRedFinal));

            if (isPlayoff && tbaPenaltyFreePlayoff)
                tbaPenaltyFreeHighScorePlayoff.Add(match);

            if (isQual && tbaPenaltyFreeQual)
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
            var (match, alliance) = FindHighestScore(overallHighScorePlayoff);
            highScoresData.Add(BuildHighScoreJson(year, "overall", "playoff", match, alliance, prefix));
        }

        if (overallHighScoreQual.Count > 0)
        {
            var (match, alliance) = FindHighestScore(overallHighScoreQual);
            highScoresData.Add(BuildHighScoreJson(year, "overall", "qual", match, alliance, prefix));
        }

        if (penaltyFreeHighScorePlayoff.Count > 0)
        {
            var (match, alliance) = FindHighestScore(penaltyFreeHighScorePlayoff);
            highScoresData.Add(BuildHighScoreJson(year, "penaltyFree", "playoff", match, alliance, prefix));
        }

        if (penaltyFreeHighScoreQual.Count > 0)
        {
            var (match, alliance) = FindHighestScore(penaltyFreeHighScoreQual);
            highScoresData.Add(BuildHighScoreJson(year, "penaltyFree", "qual", match, alliance, prefix));
        }

        if (offsettingPenaltyHighScorePlayoff.Count > 0)
        {
            var (match, alliance) = FindHighestScore(offsettingPenaltyHighScorePlayoff);
            highScoresData.Add(BuildHighScoreJson(year, "offsetting", "playoff", match, alliance, prefix));
        }

        if (offsettingPenaltyHighScoreQual.Count > 0)
        {
            var (match, alliance) = FindHighestScore(offsettingPenaltyHighScoreQual);
            highScoresData.Add(BuildHighScoreJson(year, "offsetting", "qual", match, alliance, prefix));
        }

        if (tbaPenaltyFreeHighScoreQual.Count > 0)
        {
            var (match, alliance) = FindHighestScore(tbaPenaltyFreeHighScoreQual);
            highScoresData.Add(BuildHighScoreJson(year, "TBAPenaltyFree", "qual", match, alliance, prefix));
        }

        if (tbaPenaltyFreeHighScorePlayoff.Count > 0)
        {
            var (match, alliance) = FindHighestScore(tbaPenaltyFreeHighScorePlayoff);
            highScoresData.Add(BuildHighScoreJson(year, "TBAPenaltyFree", "playoff", match, alliance, prefix));
        }

        if (allianceContributionHighScorePlayoff.Count > 0)
        {
            var (match, alliance) = FindHighestPenaltyDeductedScore(allianceContributionHighScorePlayoff, isFtc);
            highScoresData.Add(BuildHighScoreJson(year, "allianceContribution", "playoff", match, alliance, prefix));
        }

        if (allianceContributionHighScoreQual.Count > 0)
        {
            var (match, alliance) = FindHighestPenaltyDeductedScore(allianceContributionHighScoreQual, isFtc);
            highScoresData.Add(BuildHighScoreJson(year, "allianceContribution", "qual", match, alliance, prefix));
        }

        return highScoresData;
    }

    private static (HybridMatch match, string alliance) FindHighestScore(IEnumerable<HybridMatch> matches)
    {
        var matchList = matches.ToList();
        if (matchList.Count == 0) return (new HybridMatch(), "");

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

        return (bestMatch.Match, bestMatch.WinningAlliance);
    }

    private static (HybridMatch match, string alliance) FindHighestPenaltyDeductedScore(
        IEnumerable<HybridMatch> matches, bool isFtc)
    {
        var matchList = matches.ToList();
        if (matchList.Count == 0) return (new HybridMatch(), "");

        // Find the match with the highest score after deducting penalty points received.
        // FRC: allianceFoul = points that alliance received from opponent fouls → deduct from own final.
        // FTC: allianceFoul = points that alliance committed → deduct opponent's foul from own final.
        var bestMatch = matchList
            .Select(m =>
            {
                var blueFinal = m.ScoreBlueFinal ?? 0;
                var redFinal = m.ScoreRedFinal ?? 0;
                var blueFoul = m.ScoreBlueFoul ?? 0;
                var redFoul = m.ScoreRedFoul ?? 0;

                var blueDeducted = isFtc
                    ? Math.Max(0, blueFinal - redFoul)   // FTC: blue received red's committed fouls
                    : Math.Max(0, blueFinal - blueFoul); // FRC: blueFoul = points blue received
                var redDeducted = isFtc
                    ? Math.Max(0, redFinal - blueFoul)   // FTC: red received blue's committed fouls
                    : Math.Max(0, redFinal - redFoul);   // FRC: redFoul = points red received

                return new
                {
                    Match = m,
                    BlueDeducted = blueDeducted,
                    RedDeducted = redDeducted,
                    HighestDeducted = Math.Max(blueDeducted, redDeducted),
                    BestAlliance = blueDeducted >= redDeducted ? "blue" : "red"
                };
            })
            .OrderByDescending(x => x.HighestDeducted)
            .First();

        return (bestMatch.Match, bestMatch.BestAlliance);
    }

    private static HighScore BuildHighScoreJson(int year, string category, string tournamentLevel,
        HybridMatch match, string alliance, string? prefix)
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