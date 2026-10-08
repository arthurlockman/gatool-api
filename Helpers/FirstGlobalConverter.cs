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
    private static string StationToFrcStation(int station)
    {
        return station switch
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
    }

    /// <summary>
    ///     Maps a FIRST Global tournamentKey to an FRC-style tournament level string.
    /// </summary>
    private static string TournamentKeyToLevel(string tournamentKey)
    {
        return tournamentKey switch
        {
            "t2" => "Qualification",
            "t3" => "Playoff",
            "t4" => "Finals",
            _ => tournamentKey
        };
    }

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
            t.TeamKey,
            t.Name,
            string.IsNullOrEmpty(t.ShortName) ? null : t.ShortName,
            null,
            null,
            t.Country,
            t.CountryCode,
            0,
            null,
            null,
            null,
            null,
            null
        )).ToList();

        return new FgTeamsResponse(
            frcTeams.Count,
            frcTeams.Count,
            1,
            1,
            frcTeams
        );
    }

    /// <summary>
    ///     Converts a list of FIRST Global matches to an FRC <see cref="MatchesResponse" />.
    ///     Stations are mapped to FRC-style strings (Red1/Blue1 etc.), tournament keys are
    ///     expanded to level names, and score/foul fields are mapped to FRC conventions.
    ///     <c>ScoreRedFoul</c> carries foul points awarded to Red (i.e. from Blue's fouls),
    ///     and vice-versa for <c>ScoreBlueFoul</c>.
    /// </summary>
    public static FgMatchesResponse ToFrcMatches(List<FgMatch> matches)
    {
        var fgMatches = matches.Select(m =>
        {
            var matchNumber = ExtractMatchNumber(m);
            var tournamentLevel = TournamentKeyToLevel(m.TournamentKey);

            var teams = (m.Participants ?? []).Select(p => new MatchTeam(
                p.TeamKey,
                StationToFrcStation(p.Station),
                p.Disqualified != 0
            )).ToList();

            return new FgMatchResult(
                false,
                null,
                $"{tournamentLevel} {matchNumber}",
                matchNumber,
                m.RedScore,
                m.BlueMinPen + m.BlueMajPen,
                null,
                m.BlueScore,
                m.RedMinPen + m.RedMajPen,
                null,
                m.ScheduledTime,
                string.IsNullOrEmpty(m.StartTime) ? null : m.StartTime,
                tournamentLevel,
                null,
                m.FieldNumber,
                teams
            );
        }).ToList();

        return new FgMatchesResponse(fgMatches);
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
            r.Rank,
            r.TeamKey,
            r.RankingScore,
            r.HighestScore,
            r.ProtectionPoints,
            0,
            0,
            0,
            r.Wins,
            r.Losses,
            r.Ties,
            r.RankingScore,
            0,
            r.Played
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
                    "Red",
                    m.RedScore,
                    m.BlueMinPen + m.BlueMajPen,
                    d.BarriersInRedMitigator,
                    d.RedRobotOneParking,
                    d.RedRobotTwoParking,
                    d.RedRobotThreeParking,
                    d.RedProtectionMultiplier,
                    d.BiodiversityUnitsRedSideEcosystem,
                    d.ApproximateBiodiversityRedSideEcosystem
                );

                var blue = new FgAllianceScore(
                    "Blue",
                    m.BlueScore,
                    m.RedMinPen + m.RedMajPen,
                    d.BarriersInBlueMitigator,
                    d.BlueRobotOneParking,
                    d.BlueRobotTwoParking,
                    d.BlueRobotThreeParking,
                    d.BlueProtectionMultiplier,
                    d.BiodiversityUnitsBlueSideEcosystem,
                    d.ApproximateBiodiversityBlueSideEcosystem
                );

                return new FgMatchScore(
                    tournamentLevel,
                    matchNumber,
                    m.Result,
                    d.Coopertition != 0,
                    d.AllBarriersCleared != 0,
                    d.BiodiversityDistributed,
                    d.BiodiversityDistributionFactor,
                    d.BiodiversityUnitsCenterEcosystem,
                    d.ApproximateBiodiversityCenterEcosystem,
                    [red, blue]
                )
                {
                    AdditionalProperties = d.AdditionalProperties
                };
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
            a.Rank,
            a.Captain?.TeamKey ?? 0,
            a.Pick1?.TeamKey ?? 0,
            (object?)a.Pick2?.TeamKey,
            (object?)a.Pick3?.TeamKey,
            null,
            null,
            a.Name
        )).ToList();

        return new AlliancesResponse(frcAlliances, frcAlliances.Count);
    }

    /// <summary>
    ///     Maps a gold/silver/bronze tier name to the FRC-style <c>Series</c> value (1/2/3).
    ///     Returns null for untiered recipients (e.g. Safety Award).
    /// </summary>
    private static int? ClassToSeries(string? tierClass)
    {
        return tierClass?.ToLowerInvariant() switch
        {
            "gold" => 1,
            "silver" => 2,
            "bronze" => 3,
            _ => null
        };
    }

    private static IEnumerable<(FgAward Award, FgAwardRecipient Recipient, int? Series)> AwardRecipients(
        IEnumerable<FgAward> awards)
    {
        foreach (var award in awards)
        {
            if (award.Gold != null) yield return (award, award.Gold, 1);
            if (award.Silver != null) yield return (award, award.Silver, 2);
            if (award.Bronze != null) yield return (award, award.Bronze, 3);

            foreach (var other in award.Other ?? [])
                yield return (award, other, ClassToSeries(other.Class));
        }
    }

    private static Award ToFrcAward(FgAward award, FgAwardRecipient recipient, int? series, int? teamNumber)
    {
        return new Award(
            award.SortOrder,
            null,
            null,
            null,
            award.EventKey,
            award.Name,
            series,
            teamNumber,
            null,
            recipient.Country,
            recipient.RecipientName,
            null,
            null
        );
    }

    /// <summary>
    ///     Converts a list of FIRST Global awards to an FRC <see cref="EventAwardsResponse" />.
    ///     Each gold/silver/bronze/other recipient becomes its own <see cref="Award" /> row, matching
    ///     the FRC convention of one row per (award, recipient). <c>TeamNumber</c> is resolved by
    ///     matching the recipient's country code against the season's teams; recipients without a
    ///     matching team (e.g. individual mentor awards) get a null <c>TeamNumber</c>.
    /// </summary>
    public static EventAwardsResponse ToFrcAwards(List<FgAward> awards, List<FgTeam> teams)
    {
        var teamNumberByCountryCode = teams
            .Where(t => !string.IsNullOrEmpty(t.CountryCode))
            .GroupBy(t => t.CountryCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().TeamKey, StringComparer.OrdinalIgnoreCase);

        var result = new List<Award>();

        foreach (var (award, recipient, series) in AwardRecipients(awards))
        {
            var teamNumber = recipient.CountryCode != null &&
                             teamNumberByCountryCode.TryGetValue(recipient.CountryCode, out var tn)
                ? tn
                : (int?)null;
            result.Add(ToFrcAward(award, recipient, series, teamNumber));
        }

        return new EventAwardsResponse(result);
    }

    /// <summary>
    ///     Returns the distinct country codes represented by country-based award recipients.
    /// </summary>
    public static IEnumerable<string> AwardCountryCodes(IEnumerable<FgAward> awards)
    {
        return AwardRecipients(awards)
            .Select(item => item.Recipient.CountryCode)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code!.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Groups one season's awards by <c>countryCode</c> while resolving every award row to the
    ///     team number used by that country code in the requested (display) season. The separate
    ///     <c>country</c> value remains the human-readable name in <c>FullTeamName</c>.
    /// </summary>
    public static Dictionary<string, TeamAwardsResponse> ToFrcAwardsByCountryCode(
        IEnumerable<FgAward> awards,
        IEnumerable<FgTeam> requestedYearTeams,
        IEnumerable<string> countryCodes)
    {
        var normalizedCodes = countryCodes
            .Select(code => code.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var selectedCodes = normalizedCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var teamNumberByCountryCode = requestedYearTeams
            .Where(team => !string.IsNullOrWhiteSpace(team.CountryCode))
            .GroupBy(team => team.CountryCode.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().TeamKey, StringComparer.OrdinalIgnoreCase);
        var groupedAwards = normalizedCodes.ToDictionary(
            code => code,
            _ => new List<Award>(),
            StringComparer.OrdinalIgnoreCase);

        foreach (var (award, recipient, series) in AwardRecipients(awards))
        {
            var countryCode = recipient.CountryCode?.Trim();
            if (string.IsNullOrEmpty(countryCode) || !selectedCodes.Contains(countryCode)) continue;

            var teamNumber = teamNumberByCountryCode.TryGetValue(countryCode, out var tn) ? tn : (int?)null;
            groupedAwards[countryCode].Add(ToFrcAward(award, recipient, series, teamNumber));
        }

        return groupedAwards.ToDictionary(
            pair => pair.Key,
            pair => new TeamAwardsResponse(pair.Value),
            StringComparer.OrdinalIgnoreCase);
    }
}