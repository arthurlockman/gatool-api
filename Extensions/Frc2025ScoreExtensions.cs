using System.Text.Json;
using GAToolAPI.Models;
using Microsoft.Extensions.Logging;

namespace GAToolAPI.Extensions;

/// <summary>
/// Score breakdown transformation logic specific to the 2025 FRC season (Reefscape).
/// TBA returns a per-alliance score_breakdown object for this season which must be
/// mapped onto the typed <see cref="MatchScore"/> / <see cref="AllianceScore"/> models.
/// For 2026 and beyond the raw JSON from FIRST/TBA is forwarded directly, so no
/// equivalent class is needed for future seasons.
/// </summary>
public static class Frc2025ScoreExtensions
{
    /// <summary>
    /// Transforms a TBA score_breakdown object into the 2025-season typed <see cref="MatchScore"/>.
    /// Returns <c>null</c> if <paramref name="scoreBreakdown"/> is null or cannot be parsed.
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
                MatchLevel: tournamentLevel == "Qual" ? "Qualification" : "Playoff",
                MatchNumber: matchNumber,
                WinningAlliance: DetermineWinningAlliance(tbaAlliances),
                Tiebreaker: new Tiebreaker(-1, ""),
                CoopertitionBonusAchieved: coopertitionAchieved,
                Alliances: alliances
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

    private static AllianceScore? TransformAllianceScore(JsonElement allianceElement, string allianceName, ILogger? logger)
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
                Alliance: allianceName,
                AutoLineRobot1: GetStringValue(allianceElement, "autoLineRobot1"),
                EndGameRobot1: GetStringValue(allianceElement, "endGameRobot1"),
                AutoLineRobot2: GetStringValue(allianceElement, "autoLineRobot2"),
                EndGameRobot2: GetStringValue(allianceElement, "endGameRobot2"),
                AutoLineRobot3: GetStringValue(allianceElement, "autoLineRobot3"),
                EndGameRobot3: GetStringValue(allianceElement, "endGameRobot3"),
                AutoReef: autoReef,
                AutoCoralCount: GetIntValue(allianceElement, "autoCoralCount"),
                AutoMobilityPoints: GetIntValue(allianceElement, "autoMobilityPoints"),
                AutoPoints: GetIntValue(allianceElement, "autoPoints"),
                AutoCoralPoints: GetIntValue(allianceElement, "autoCoralPoints"),
                TeleopReef: teleopReef,
                TeleopCoralCount: GetIntValue(allianceElement, "teleopCoralCount"),
                TeleopPoints: GetIntValue(allianceElement, "teleopPoints"),
                TeleopCoralPoints: GetIntValue(allianceElement, "teleopCoralPoints"),
                AlgaePoints: GetIntValue(allianceElement, "algaePoints"),
                NetAlgaeCount: GetIntValue(allianceElement, "netAlgaeCount"),
                WallAlgaeCount: GetIntValue(allianceElement, "wallAlgaeCount"),
                EndGameBargePoints: GetIntValue(allianceElement, "endGameBargePoints"),
                AutoBonusAchieved: GetBoolValue(allianceElement, "autoBonusAchieved"),
                CoralBonusAchieved: GetBoolValue(allianceElement, "coralBonusAchieved"),
                BargeBonusAchieved: GetBoolValue(allianceElement, "bargeBonusAchieved"),
                CoopertitionCriteriaMet: GetBoolValue(allianceElement, "coopertitionCriteriaMet"),
                FoulCount: GetIntValue(allianceElement, "foulCount"),
                TechFoulCount: GetIntValue(allianceElement, "techFoulCount"),
                G206Penalty: GetBoolValue(allianceElement, "g206Penalty"),
                G410Penalty: GetBoolValue(allianceElement, "g410Penalty"),
                G418Penalty: GetBoolValue(allianceElement, "g418Penalty"),
                G428Penalty: GetBoolValue(allianceElement, "g428Penalty"),
                AdjustPoints: 0, // TBA doesn't provide this
                FoulPoints: GetIntValue(allianceElement, "foulPoints"),
                Rp: GetIntValue(allianceElement, "rp"),
                TotalPoints: GetIntValue(allianceElement, "totalPoints")
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

    private static ReefRow ParseReefRow(JsonElement rowElement) =>
        new(
            NodeA: GetBoolValue(rowElement, "nodeA"),
            NodeB: GetBoolValue(rowElement, "nodeB"),
            NodeC: GetBoolValue(rowElement, "nodeC"),
            NodeD: GetBoolValue(rowElement, "nodeD"),
            NodeE: GetBoolValue(rowElement, "nodeE"),
            NodeF: GetBoolValue(rowElement, "nodeF"),
            NodeG: GetBoolValue(rowElement, "nodeG"),
            NodeH: GetBoolValue(rowElement, "nodeH"),
            NodeI: GetBoolValue(rowElement, "nodeI"),
            NodeJ: GetBoolValue(rowElement, "nodeJ"),
            NodeK: GetBoolValue(rowElement, "nodeK"),
            NodeL: GetBoolValue(rowElement, "nodeL")
        );

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

    private static string? GetStringValue(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;

    private static int GetIntValue(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.Number
            ? prop.GetInt32()
            : 0;

    private static bool GetBoolValue(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var prop) &&
        prop.ValueKind is JsonValueKind.True or JsonValueKind.False &&
        prop.GetBoolean();

    private static string ToCamelCase(string str) =>
        string.IsNullOrEmpty(str) || char.IsLower(str[0])
            ? str
            : char.ToLower(str[0]) + str[1..];
}

