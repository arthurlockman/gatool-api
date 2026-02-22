using System.Text.Json;
using GAToolAPI.Models;

namespace GAToolAPI.Extensions;

/// <summary>
///     Score breakdown transformation logic specific to the 2025 FRC season (Reefscape).
///     TBA returns a per-alliance score_breakdown object for this season which must be
///     mapped onto the typed <see cref="MatchScore" /> / <see cref="AllianceScore" /> models.
///     For 2026 and beyond the raw JSON from FIRST/TBA is forwarded directly, so no
///     equivalent class is needed for future seasons.
/// </summary>
public static class Frc2025ScoreExtensions
{
    /// <summary>
    ///     Transforms a TBA score_breakdown object into the 2025-season typed <see cref="MatchScore" />.
    ///     Returns <c>null</c> if <paramref name="scoreBreakdown" /> is null or cannot be parsed.
    /// </summary>
    public static MatchScore? Transform2025ScoreBreakdown(
        object? scoreBreakdown,
        Dictionary<string, TBAMatchAlliance>? tbaAlliances,
        int matchNumber,
        string tournamentLevel,
        ILogger? logger = null)
    {
        if (scoreBreakdown == null) return null;

        try
        {
            var breakdown = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                JsonSerializer.Serialize(scoreBreakdown));

            if (breakdown == null) return null;

            var alliances = new List<AllianceScore>();

            if (breakdown.TryGetValue("blue", out var blueElement))
            {
                var blueAlliance = TransformAllianceScore(blueElement, "Blue", logger);
                if (blueAlliance != null) alliances.Add(blueAlliance);
            }

            if (breakdown.TryGetValue("red", out var redElement))
            {
                var redAlliance = TransformAllianceScore(redElement, "Red", logger);
                if (redAlliance != null) alliances.Add(redAlliance);
            }

            var coopertitionAchieved = alliances.Count == 2 && alliances.All(a => a.CoopertitionCriteriaMet);

            var matchScore = new MatchScore(
                tournamentLevel == "Qual" ? "Qualification" : "Playoff",
                matchNumber,
                DetermineWinningAlliance(tbaAlliances),
                new Tiebreaker(-1, ""),
                coopertitionAchieved,
                alliances
            )
            {
                AdditionalProperties = new Dictionary<string, object>()
            };

            ExtractBonusProperties(matchScore);

            return matchScore;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error transforming 2025 score breakdown for match {MatchNumber}", matchNumber);
            return null;
        }
    }

    private static AllianceScore? TransformAllianceScore(JsonElement allianceElement, string allianceName,
        ILogger? logger)
    {
        try
        {
            Reef? autoReef = null;
            if (allianceElement.TryGetProperty("autoReef", out var autoReefElement))
                autoReef = ParseReef(autoReefElement);

            Reef? teleopReef = null;
            if (allianceElement.TryGetProperty("teleopReef", out var teleopReefElement))
                teleopReef = ParseReef(teleopReefElement);

            return new AllianceScore(
                allianceName,
                GetStringValue(allianceElement, "autoLineRobot1"),
                GetStringValue(allianceElement, "endGameRobot1"),
                GetStringValue(allianceElement, "autoLineRobot2"),
                GetStringValue(allianceElement, "endGameRobot2"),
                GetStringValue(allianceElement, "autoLineRobot3"),
                GetStringValue(allianceElement, "endGameRobot3"),
                autoReef,
                GetIntValue(allianceElement, "autoCoralCount"),
                GetIntValue(allianceElement, "autoMobilityPoints"),
                GetIntValue(allianceElement, "autoPoints"),
                GetIntValue(allianceElement, "autoCoralPoints"),
                teleopReef,
                GetIntValue(allianceElement, "teleopCoralCount"),
                GetIntValue(allianceElement, "teleopPoints"),
                GetIntValue(allianceElement, "teleopCoralPoints"),
                GetIntValue(allianceElement, "algaePoints"),
                GetIntValue(allianceElement, "netAlgaeCount"),
                GetIntValue(allianceElement, "wallAlgaeCount"),
                GetIntValue(allianceElement, "endGameBargePoints"),
                GetBoolValue(allianceElement, "autoBonusAchieved"),
                GetBoolValue(allianceElement, "coralBonusAchieved"),
                GetBoolValue(allianceElement, "bargeBonusAchieved"),
                GetBoolValue(allianceElement, "coopertitionCriteriaMet"),
                GetIntValue(allianceElement, "foulCount"),
                GetIntValue(allianceElement, "techFoulCount"),
                GetBoolValue(allianceElement, "g206Penalty"),
                GetBoolValue(allianceElement, "g410Penalty"),
                GetBoolValue(allianceElement, "g418Penalty"),
                GetBoolValue(allianceElement, "g428Penalty"),
                0, // TBA doesn't provide this
                GetIntValue(allianceElement, "foulPoints"),
                GetIntValue(allianceElement, "rp"),
                GetIntValue(allianceElement, "totalPoints")
            );
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error transforming 2025 alliance score for {AllianceName}", allianceName);
            return null;
        }
    }

    private static Reef ParseReef(JsonElement reefElement)
    {
        ReefRow? topRow = null;
        if (reefElement.TryGetProperty("topRow", out var topRowElement))
            topRow = ParseReefRow(topRowElement);

        ReefRow? midRow = null;
        if (reefElement.TryGetProperty("midRow", out var midRowElement))
            midRow = ParseReefRow(midRowElement);

        ReefRow? botRow = null;
        if (reefElement.TryGetProperty("botRow", out var botRowElement))
            botRow = ParseReefRow(botRowElement);

        return new Reef(topRow, midRow, botRow, GetIntValue(reefElement, "trough"));
    }

    private static ReefRow ParseReefRow(JsonElement rowElement)
    {
        return new ReefRow(
            GetBoolValue(rowElement, "nodeA"),
            GetBoolValue(rowElement, "nodeB"),
            GetBoolValue(rowElement, "nodeC"),
            GetBoolValue(rowElement, "nodeD"),
            GetBoolValue(rowElement, "nodeE"),
            GetBoolValue(rowElement, "nodeF"),
            GetBoolValue(rowElement, "nodeG"),
            GetBoolValue(rowElement, "nodeH"),
            GetBoolValue(rowElement, "nodeI"),
            GetBoolValue(rowElement, "nodeJ"),
            GetBoolValue(rowElement, "nodeK"),
            GetBoolValue(rowElement, "nodeL")
        );
    }

    private static void ExtractBonusProperties(MatchScore matchScore)
    {
        if (matchScore.Alliances == null || matchScore.Alliances.Count == 0) return;
        if (matchScore.AdditionalProperties == null) return;

        var bonusProperties = typeof(AllianceScore).GetProperties()
            .Where(p => p.Name.Contains("Bonus", StringComparison.OrdinalIgnoreCase) &&
                        p.Name != "CoopertitionCriteriaMet")
            .ToList();

        foreach (var property in bonusProperties)
        {
            var jsonPropertyName = ToCamelCase(property.Name);

            if (property.PropertyType == typeof(bool))
            {
                var anyAchieved = matchScore.Alliances
                    .Select(a => (bool?)property.GetValue(a))
                    .Any(v => v == true);
                matchScore.AdditionalProperties[jsonPropertyName] = anyAchieved;
            }
            else
            {
                var value = property.GetValue(matchScore.Alliances[0]);
                if (value != null)
                    matchScore.AdditionalProperties[jsonPropertyName] = value;
            }
        }
    }

    private static int? DetermineWinningAlliance(Dictionary<string, TBAMatchAlliance>? alliances)
    {
        if (alliances == null) return null;
        var blueScore = alliances.TryGetValue("blue", out var blue) ? blue.Score : 0;
        var redScore = alliances.TryGetValue("red", out var red) ? red.Score : 0;
        if (blueScore > redScore) return 0;
        if (redScore > blueScore) return 1;
        return -1;
    }

    private static string? GetStringValue(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;
    }

    private static int GetIntValue(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.Number
            ? prop.GetInt32()
            : 0;
    }

    private static bool GetBoolValue(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var prop) &&
               prop.ValueKind is JsonValueKind.True or JsonValueKind.False &&
               prop.GetBoolean();
    }

    private static string ToCamelCase(string str)
    {
        return string.IsNullOrEmpty(str) || char.IsLower(str[0])
            ? str
            : char.ToLowerInvariant(str[0]) + str[1..];
    }
}