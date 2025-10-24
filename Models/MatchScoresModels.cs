using System.Text.Json.Serialization;

namespace GAToolAPI.Models;

public class ReefRow
{
    [JsonPropertyName("nodeA")] public bool NodeA { get; set; }
    [JsonPropertyName("nodeB")] public bool NodeB { get; set; }
    [JsonPropertyName("nodeC")] public bool NodeC { get; set; }
    [JsonPropertyName("nodeD")] public bool NodeD { get; set; }
    [JsonPropertyName("nodeE")] public bool NodeE { get; set; }
    [JsonPropertyName("nodeF")] public bool NodeF { get; set; }
    [JsonPropertyName("nodeG")] public bool NodeG { get; set; }
    [JsonPropertyName("nodeH")] public bool NodeH { get; set; }
    [JsonPropertyName("nodeI")] public bool NodeI { get; set; }
    [JsonPropertyName("nodeJ")] public bool NodeJ { get; set; }
    [JsonPropertyName("nodeK")] public bool NodeK { get; set; }
    [JsonPropertyName("nodeL")] public bool NodeL { get; set; }
}

public class ReefGrid
{
    [JsonPropertyName("topRow")] public ReefRow TopRow { get; set; } = new();
    [JsonPropertyName("midRow")] public ReefRow MidRow { get; set; } = new();
    [JsonPropertyName("botRow")] public ReefRow BotRow { get; set; } = new();
    [JsonPropertyName("trough")] public int Trough { get; set; }
}

public class MatchScoreAlliance
{
    // NOTE: Any properties with "Bonus" in the name will be automatically extracted 
    // and surfaced at the top level of the MatchScore object to support year-over-year game changes.
    // Update this class each season with the new game's scoring properties.
    
    [JsonPropertyName("alliance")] public string Alliance { get; set; } = string.Empty;
    [JsonPropertyName("autoLineRobot1")] public string? AutoLineRobot1 { get; set; }
    [JsonPropertyName("endGameRobot1")] public string? EndGameRobot1 { get; set; }
    [JsonPropertyName("autoLineRobot2")] public string? AutoLineRobot2 { get; set; }
    [JsonPropertyName("endGameRobot2")] public string? EndGameRobot2 { get; set; }
    [JsonPropertyName("autoLineRobot3")] public string? AutoLineRobot3 { get; set; }
    [JsonPropertyName("endGameRobot3")] public string? EndGameRobot3 { get; set; }
    [JsonPropertyName("autoReef")] public ReefGrid AutoReef { get; set; } = new();
    [JsonPropertyName("autoCoralCount")] public int AutoCoralCount { get; set; }
    [JsonPropertyName("autoMobilityPoints")] public int AutoMobilityPoints { get; set; }
    [JsonPropertyName("autoPoints")] public int AutoPoints { get; set; }
    [JsonPropertyName("autoCoralPoints")] public int AutoCoralPoints { get; set; }
    [JsonPropertyName("teleopReef")] public ReefGrid TeleopReef { get; set; } = new();
    [JsonPropertyName("teleopCoralCount")] public int TeleopCoralCount { get; set; }
    [JsonPropertyName("teleopPoints")] public int TeleopPoints { get; set; }
    [JsonPropertyName("teleopCoralPoints")] public int TeleopCoralPoints { get; set; }
    [JsonPropertyName("algaePoints")] public int AlgaePoints { get; set; }
    [JsonPropertyName("netAlgaeCount")] public int NetAlgaeCount { get; set; }
    [JsonPropertyName("wallAlgaeCount")] public int WallAlgaeCount { get; set; }
    [JsonPropertyName("endGameBargePoints")] public int EndGameBargePoints { get; set; }
    [JsonPropertyName("autoBonusAchieved")] public bool AutoBonusAchieved { get; set; }
    [JsonPropertyName("coralBonusAchieved")] public bool CoralBonusAchieved { get; set; }
    [JsonPropertyName("bargeBonusAchieved")] public bool BargeBonusAchieved { get; set; }
    [JsonPropertyName("coopertitionCriteriaMet")] public bool CoopertitionCriteriaMet { get; set; }
    [JsonPropertyName("foulCount")] public int FoulCount { get; set; }
    [JsonPropertyName("techFoulCount")] public int TechFoulCount { get; set; }
    [JsonPropertyName("g206Penalty")] public bool G206Penalty { get; set; }
    [JsonPropertyName("g410Penalty")] public bool G410Penalty { get; set; }
    [JsonPropertyName("g418Penalty")] public bool G418Penalty { get; set; }
    [JsonPropertyName("g428Penalty")] public bool G428Penalty { get; set; }
    [JsonPropertyName("adjustPoints")] public int AdjustPoints { get; set; }
    [JsonPropertyName("foulPoints")] public int FoulPoints { get; set; }
    [JsonPropertyName("rp")] public int Rp { get; set; }
    [JsonPropertyName("totalPoints")] public int TotalPoints { get; set; }
}

public class Tiebreaker
{
    [JsonPropertyName("item1")] public int Item1 { get; set; }
    [JsonPropertyName("item2")] public string Item2 { get; set; } = string.Empty;
}

public class MatchScore
{
    // Core properties that remain consistent across seasons
    [JsonPropertyName("matchLevel")] public string MatchLevel { get; set; } = string.Empty;
    [JsonPropertyName("matchNumber")] public int MatchNumber { get; set; }
    [JsonPropertyName("winningAlliance")] public int? WinningAlliance { get; set; }
    [JsonPropertyName("tiebreaker")] public Tiebreaker? Tiebreaker { get; set; }
    [JsonPropertyName("coopertitionBonusAchieved")] public bool CoopertitionBonusAchieved { get; set; }
    [JsonPropertyName("alliances")] public List<MatchScoreAlliance> Alliances { get; set; } = new();
    
    // Dynamic properties for game-specific bonuses (extracted from alliance details)
    [JsonExtensionData]
    public Dictionary<string, object>? AdditionalProperties { get; set; }
}

