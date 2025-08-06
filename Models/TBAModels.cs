namespace GAToolAPI.Models;

public class TBATeam
{
    public string Key { get; set; } = string.Empty;
    public int TeamNumber { get; set; }
    public string Nickname { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? SchoolName { get; set; }
    public string City { get; set; } = string.Empty;
    public string StateProv { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? PostalCode { get; set; }
    public string? GmapsPlaceId { get; set; }
    public string? GmapsUrl { get; set; }
    public double? Lat { get; set; }
    public double? Lng { get; set; }
    public string? LocationName { get; set; }
    public string? Website { get; set; }
    public int RookieYear { get; set; }
}

public class TBADistrict
{
    public string Abbreviation { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public int Year { get; set; }
}

public class TBAWebcast
{
    public string Type { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string File { get; set; } = string.Empty;
}

public class TBAEvent
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string EventCode { get; set; } = string.Empty;
    public int EventType { get; set; }
    public TBADistrict? District { get; set; }
    public string City { get; set; } = string.Empty;
    public string StateProv { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
    public int Year { get; set; }
    public string ShortName { get; set; } = string.Empty;
    public string EventTypeString { get; set; } = string.Empty;
    public int? Week { get; set; }
    public string? Address { get; set; }
    public string? PostalCode { get; set; }
    public string? GmapsPlaceId { get; set; }
    public string? GmapsUrl { get; set; }
    public double? Lat { get; set; }
    public double? Lng { get; set; }
    public string? LocationName { get; set; }
    public string? Timezone { get; set; }
    public string? Website { get; set; }
    public string? FirstEventId { get; set; }
    public string? FirstEventCode { get; set; }
    public List<TBAWebcast> Webcasts { get; set; } = new();
    public List<string> DivisionKeys { get; set; } = new();
    public string? ParentEventKey { get; set; }
    public int? PlayoffType { get; set; }
    public string? PlayoffTypeString { get; set; }
}