using GAToolAPI.Models;

namespace GAToolAPI.Helpers;

/// <summary>
///     Converts raw FIRST Global API responses to FRC-compatible response shapes.
/// </summary>
public static class FirstGlobalConverter
{
    /// <summary>
    ///     Maps a FIRST Global station number to an FRC-style station string.
    ///     Stations 11-14 → Red1-Red4; stations 21-24 → Blue1-Blue4.
    /// </summary>
    private static string StationToFrcStation(int station) => station switch
    {
        11 => "Red1",
        12 => "Red2",
        13 => "Red3",
        14 => "Red4",
        21 => "Blue1",
        22 => "Blue2",
        23 => "Blue3",
        24 => "Blue4",
        _ => station.ToString()
    };

    /// <summary>
    ///     Maps a FIRST Global tournamentKey to an FRC-style tournament level string.
    /// </summary>
    private static string TournamentKeyToLevel(string tournamentKey) => tournamentKey switch
    {
        "t2" => "Qualification",
        "t3" => "Playoff",
        "t4" => "Finals",
        _ => tournamentKey
    };

    /// <summary>
    ///     Extracts the match number from the match name (e.g. "Ranking Match 15" → 15).
    ///     Falls back to the match id if parsing fails.
    /// </summary>
    private static int ExtractMatchNumber(FgMatch match)
    {
        var parts = match.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 0 && int.TryParse(parts[^1], out var n))
            return n;
        return match.Id;
    }

    /// <summary>
    ///     Converts a list of FIRST Global teams to an FRC <see cref="TeamsResponse" />.
    ///     Each team's <c>teamKey</c> becomes <c>TeamNumber</c> and the country name becomes
    ///     both <c>NameFull</c> and <c>Country</c>.
    /// </summary>
    public static FgTeamsResponse ToFrcTeams(List<FgTeam> teams)
    {
        var frcTeams = teams.Select(t => new FgFrcTeam(
            TeamNumber: t.TeamKey,
            NameFull: t.Name,
            NameShort: string.IsNullOrEmpty(t.ShortName) ? null : t.ShortName,
            City: null,
            StateProv: null,
            Country: t.Country,
            CountryCode: t.CountryCode,
            RookieYear: 0,
            RobotName: null,
            DistrictCode: null,
            SchoolName: null,
            Website: null,
            HomeCMP: null
        )).ToList();

        return new FgTeamsResponse(
            TeamCountTotal: frcTeams.Count,
            TeamCountPage: frcTeams.Count,
            PageCurrent: 1,
            PageTotal: 1,
            Teams: frcTeams
        );
    }

    /// <summary>
    ///     Converts a list of FIRST Global matches to an FRC <see cref="MatchesResponse" />.
    ///     Stations are mapped to FRC-style strings (Red1/Blue1 etc.), tournament keys are
    ///     expanded to level names, and score/foul fields are mapped to FRC conventions.
    ///     <c>ScoreRedFoul</c> carries foul points awarded to Red (i.e. from Blue's fouls),
    ///     and vice-versa for <c>ScoreBlueFoul</c>.
    /// </summary>
    public static MatchesResponse ToFrcMatches(List<FgMatch> matches)
    {
        var frcMatches = matches.Select(m =>
        {
            var matchNumber = ExtractMatchNumber(m);
            var tournamentLevel = TournamentKeyToLevel(m.TournamentKey);

            var teams = (m.Participants ?? []).Select(p => new MatchTeam(
                TeamNumber: p.TeamKey,
                Station: StationToFrcStation(p.Station),
                Dq: p.Disqualified != 0
            )).ToList();

            return new MatchResult(
                IsReplay: false,
                MatchVideoLink: null,
                Description: $"{tournamentLevel} {matchNumber}",
                MatchNumber: matchNumber,
                ScoreRedFinal: m.RedScore,
                ScoreRedFoul: m.BlueMinPen + m.BlueMajPen,
                ScoreRedAuto: null,
                ScoreBlueFinal: m.BlueScore,
                ScoreBlueFoul: m.RedMinPen + m.RedMajPen,
                ScoreBlueAuto: null,
                AutoStartTime: m.ScheduledTime,
                ActualStartTime: string.IsNullOrEmpty(m.StartTime) ? null : m.StartTime,
                TournamentLevel: tournamentLevel,
                PostResultTime: null,
                Teams: teams
            );
        }).ToList();

        return new MatchesResponse(frcMatches);
    }

    /// <summary>
    ///     Converts a list of FIRST Global rankings to an FRC-compatible rankings object.
    ///     Returns <c>{ rankings: { rankings: [...] } }</c> to match the FRC rankings envelope.
    ///     <c>SortOrder1</c> = ranking score, <c>SortOrder2</c> = highest score,
    ///     <c>SortOrder3</c> = protection points.
    /// </summary>
    public static object ToFrcRankings(List<FgRanking> rankings)
    {
        var frcRankings = rankings.Select(r => new TeamRanking(
            Rank: r.Rank,
            TeamNumber: r.TeamKey,
            SortOrder1: r.RankingScore,
            SortOrder2: r.HighestScore,
            SortOrder3: r.ProtectionPoints,
            SortOrder4: 0,
            SortOrder5: 0,
            SortOrder6: 0,
            Wins: r.Wins,
            Losses: r.Losses,
            Ties: r.Ties,
            QualAverage: r.RankingScore,
            Dq: 0,
            MatchesPlayed: r.Played
        )).ToList();

        return new RankingsResponse(new RankingsData(frcRankings), null);
    }

    /// <summary>
    ///     Converts a list of FIRST Global matches to an <see cref="FgMatchScoresResponse" /> containing
    ///     game-specific score breakdowns extracted from each match's <c>details</c> object.
    ///     Matches without details are omitted. <c>WinningAlliance</c> follows FRC convention:
    ///     0 = tie, 1 = Red, 2 = Blue.
    /// </summary>
    public static FgMatchScoresResponse ToFgScores(List<FgMatch> matches)
    {
        var scores = matches
            .Where(m => m.Details != null)
            .Select(m =>
            {
                var matchNumber = ExtractMatchNumber(m);
                var tournamentLevel = TournamentKeyToLevel(m.TournamentKey);
                var d = m.Details!;

                var red = new FgAllianceScore(
                    Alliance: "Red",
                    TotalPoints: m.RedScore,
                    FoulPoints: m.BlueMinPen + m.BlueMajPen,
                    BarriersInMitigator: d.BarriersInRedMitigator,
                    RobotOneParking: d.RedRobotOneParking,
                    RobotTwoParking: d.RedRobotTwoParking,
                    RobotThreeParking: d.RedRobotThreeParking,
                    ProtectionMultiplier: d.RedProtectionMultiplier,
                    BiodiversityUnits: d.BiodiversityUnitsRedSideEcosystem,
                    ApproximateBiodiversity: d.ApproximateBiodiversityRedSideEcosystem
                );

                var blue = new FgAllianceScore(
                    Alliance: "Blue",
                    TotalPoints: m.BlueScore,
                    FoulPoints: m.RedMinPen + m.RedMajPen,
                    BarriersInMitigator: d.BarriersInBlueMitigator,
                    RobotOneParking: d.BlueRobotOneParking,
                    RobotTwoParking: d.BlueRobotTwoParking,
                    RobotThreeParking: d.BlueRobotThreeParking,
                    ProtectionMultiplier: d.BlueProtectionMultiplier,
                    BiodiversityUnits: d.BiodiversityUnitsBlueSideEcosystem,
                    ApproximateBiodiversity: d.ApproximateBiodiversityBlueSideEcosystem
                );

                return new FgMatchScore(
                    MatchLevel: tournamentLevel,
                    MatchNumber: matchNumber,
                    WinningAlliance: m.Result,
                    CoopertitionAchieved: d.Coopertition != 0,
                    AllBarriersCleared: d.AllBarriersCleared != 0,
                    BiodiversityDistributed: d.BiodiversityDistributed,
                    BiodiversityDistributionFactor: d.BiodiversityDistributionFactor,
                    BiodiversityUnitsCenterEcosystem: d.BiodiversityUnitsCenterEcosystem,
                    ApproximateBiodiversityCenterEcosystem: d.ApproximateBiodiversityCenterEcosystem,
                    Alliances: [red, blue]
                );
            }).ToList();

        return new FgMatchScoresResponse(scores);
    }

    /// <summary>
    ///     Converts a list of FIRST Global alliances to an FRC <see cref="AlliancesResponse" />.
    ///     Alliance rank maps to <c>Number</c>; captain and picks map to <c>Captain</c>,
    ///     <c>Round1</c>, <c>Round2</c>, <c>Round3</c> respectively.
    /// </summary>
    public static AlliancesResponse ToFrcAlliances(List<FgAlliance> alliances)
    {
        var frcAlliances = alliances.Select(a => new Alliance(
            Number: a.Rank,
            Captain: a.Captain?.TeamKey ?? 0,
            Round1: a.Pick1?.TeamKey ?? 0,
            Round2: (object?)a.Pick2?.TeamKey,
            Round3: (object?)a.Pick3?.TeamKey,
            Backup: null,
            BackupReplaced: null,
            Name: a.Name
        )).ToList();

        return new AlliancesResponse(frcAlliances, frcAlliances.Count);
    }
}
