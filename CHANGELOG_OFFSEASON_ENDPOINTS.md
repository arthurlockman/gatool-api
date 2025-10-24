# Offseason API Endpoints - Changes and Enhancements

## Overview
This document outlines the major changes and enhancements made to support offseason FRC events through The Blue Alliance (TBA) API integration.

---

## 1. New Rankings Endpoint

### Endpoint
`GET /v3/{year}/offseason/rankings/{eventCode}`

### Description
Fetches qualification rankings for offseason FRC events from TBA and transforms them to match the FIRST API format.

### Response Format
```json
{
  "Rankings": [
    {
      "rank": 1,
      "teamNumber": 254,
      "sortOrder1": 5.2,
      "sortOrder2": 0,
      "sortOrder3": 236.1,
      "sortOrder4": 52.9,
      "sortOrder5": 23.2,
      "wins": 8,
      "losses": 2,
      "ties": 0,
      "dq": 0,
      "matchesPlayed": 10
    }
  ]
}
```

### Models Updated
- **TBAModels.cs**: Added `TBAWinLossRecord`, `TBARanking`, `TBASortOrderInfo`, `TBAEventRankings`
- Transformation converts TBA format to FIRST API `RankingsResponse` format

---

## 2. Offseason Events Endpoint Fixes

### Issues Fixed
1. **Missing Event Names**: Changed from using `evt.ShortName` to `evt.Name`
2. **Missing Events**: Removed loop starting at index 1, now processes all events using `foreach`
3. **State/Province Parsing**: Fixed address parsing to correctly extract state and postal code
4. **Event Filtering**: Updated to include Offseason, Preseason, and Unlabeled event types

### Endpoint
`GET /v3/{year}/offseason/events`

### Changes
```csharp
// Before: Started at index 1, skipping first event
for (var i = 1; i < tbaResponse.Count; i++)

// After: Process all events
foreach (var evt in tbaResponse)
```

---

## 3. Match Descriptions and Playoff Rounds

### Problem
Match descriptions and round numbers needed to match FIRST API format for proper display.

### Solution
Implemented intelligent round calculation based on tournament size (2, 4, or 8 alliances).

### Match Description Format

#### 8-Alliance Tournament
- Matches 1-4: `"Match 1 (R1)"` through `"Match 4 (R1)"`
- Matches 5-8: `"Match 5 (R2)"` through `"Match 8 (R2)"`
- Matches 9-10: `"Match 9 (R3)"`, `"Match 10 (R3)"`
- Matches 11-12: `"Match 11 (R4)"`, `"Match 12 (R4)"`
- Match 13: `"Match 13 (R5)"`
- Match 14+: `"Final 1"`, `"Final 2"`, `"Final Tiebreaker 1"`, etc.

#### 4-Alliance Tournament
- Matches 1-2: `"Match 1 (R1)"`, `"Match 2 (R1)"`
- Matches 3-4: `"Match 3 (R2)"`, `"Match 4 (R2)"`
- Match 5: `"Match 5 (R3)"`
- Match 6+: `"Final 1"`, `"Final 2"`, `"Final Tiebreaker 1"`, etc.

#### 2-Alliance Tournament
- Match 1+: `"Final 1"`, `"Final 2"`, `"Final Tiebreaker 1"`, etc.

### Implementation
- Added `GetMatchDescription()` method with switch expressions
- Tournament size detection based on quarterfinal and semifinal counts
- Sequential match numbering across all playoff matches

---

## 4. Match Time Field Population

### Problem
TBA doesn't always populate `actualStartTime`, `postResultTime`, and `autoStartTime` for matches that have been played.

### Solution
Added logic to populate missing time fields when matches have score data:

```csharp
// If match has been played but time fields are missing, populate them
if (matchHasBeenPlayed && m.Time.HasValue)
{
    var startTime = DateTimeOffset.FromUnixTimeSeconds(m.Time.Value);
    
    // Use startTime for actualStartTime if not provided
    if (hm.ActualStartTime == null)
        hm.ActualStartTime = startTime.ToString("o");
    
    // Use startTime for autoStartTime if not provided
    if (hm.AutoStartTime == null)
        hm.AutoStartTime = startTime.ToString("o");
    
    // Use startTime + 3 minutes for postResultTime if not provided
    if (hm.PostResultTime == null)
        hm.PostResultTime = startTime.AddMinutes(3).ToString("o");
}
```

---

## 5. Alliances Endpoint Transformation

### Endpoint
`GET /v3/{year}/offseason/alliances/{eventCode}`

### TBA Input Format
```json
[
  {
    "picks": ["frc3045", "frc846", "frc4765"],
    "declines": [],
    "status": { ... }
  }
]
```

### Transformed Output Format
```json
{
  "alliances": [
    {
      "number": 1,
      "captain": 3045,
      "round1": 846,
      "round2": 4765,
      "round3": null,
      "backup": null,
      "backupReplaced": null,
      "name": "Alliance 1"
    }
  ],
  "count": 8
}
```

### Models Updated
- **TBAModels.cs**: Added `TBAAllianceStatus` class
- **TBAAlliance**: Updated to match actual TBA response structure
- Transformation maps picks to captain/round1/round2/round3 format

---

## 6. Match Scores Transformation

### Problem
TBA returns a complex `score_breakdown` object that's difficult to parse on the client side. Needed to transform it to match FIRST API's simpler `MatchScores` format.

### Solution
Created comprehensive transformation system that converts TBA score breakdown to structured MatchScores format.

### New Models Created (`MatchScoresModels.cs`)

#### Core Models
- `ReefRow`: 12 nodes (A-L) for reef scoring
- `ReefGrid`: Top/Mid/Bot rows plus trough
- `MatchScoreAlliance`: Complete alliance scoring details
- `Tiebreaker`: Tiebreaker information
- `MatchScore`: Top-level match score object

### Key Features

#### 1. Season-Agnostic Design
The `MatchScore` model only includes properties that remain constant across seasons:
- `matchLevel`
- `matchNumber`
- `winningAlliance`
- `tiebreaker`
- `coopertitionBonusAchieved`
- `alliances`

#### 2. Dynamic Bonus Extraction
Uses reflection to automatically surface bonus properties from alliance details to the top level:

```csharp
// Any property with "Bonus" in the name is automatically extracted
// For 2025: autoBonusAchieved, coralBonusAchieved, bargeBonusAchieved
// For future seasons: just add new bonus properties to MatchScoreAlliance
```

#### 3. Simplified Data Structure
- Fouls and penalties are properties of each alliance (not separate objects)
- All scoring details preserved from TBA
- Easy to parse on the client side

### Transformation Methods

- `TransformScoreBreakdown()`: Main transformation entry point
- `DetermineWinningAlliance()`: Calculates winner (0=Blue, 1=Red, -1=Tie)
- `TransformAllianceScore()`: Transforms single alliance score data
- `ParseReefGrid()`: Parses reef grid structure
- `ParseReefRow()`: Parses individual reef rows
- `ExtractBonusProperties()`: Automatically surfaces bonus properties
- Helper methods for safe JSON parsing

### Example Output
```json
{
  "matchLevel": "Qualification",
  "matchNumber": 1,
  "winningAlliance": 1,
  "tiebreaker": { "item1": -1, "item2": "" },
  "coopertitionBonusAchieved": false,
  "autoBonusAchieved": true,
  "coralBonusAchieved": false,
  "bargeBonusAchieved": true,
  "alliances": [
    {
      "alliance": "Blue",
      "autoLineRobot1": "Yes",
      "endGameRobot1": "DeepCage",
      "foulCount": 0,
      "techFoulCount": 1,
      "totalPoints": 242,
      ...
    },
    {
      "alliance": "Red",
      ...
    }
  ]
}
```

---

## 7. Updated HybridMatch Model

### Changes to `HybridScheduleModels.cs`

**Before:**
```csharp
public object? ScoreBreakdown { get; set; }
public object? Fouls { get; set; }
public object? Tiebreakers { get; set; }
```

**After:**
```csharp
// Transformed score breakdown in FIRST API format
public MatchScore? MatchScores { get; set; }
```

This provides a clean, structured format instead of raw JSON objects.

---

## Future Season Updates

### To Add Support for a New Season:

1. **Update Game-Specific Models** (`MatchScoresModels.cs`):
   - Update `ReefGrid` or equivalent scoring structures for the new game
   - Update `MatchScoreAlliance` properties for new game elements
   - Name any new bonus achievements with "Bonus" in the property name

2. **Update Transformation Logic** (`FRCApiController.cs`):
   - Update `TransformAllianceScore()` to map new game-specific fields
   - The bonus extraction will automatically handle new bonus properties

3. **No Changes Needed**:
   - Core `MatchScore` structure (season-agnostic)
   - Bonus extraction logic (uses reflection)
   - Endpoint routes and caching

### Example: Adding 2026 Game Support
```csharp
// In MatchScoreAlliance class, add new properties:
[JsonPropertyName("rocketBonusAchieved")] 
public bool RocketBonusAchieved { get; set; }

[JsonPropertyName("climberBonusAchieved")] 
public bool ClimberBonusAchieved { get; set; }

// These will automatically appear at the top level of MatchScore!
```

---

## Testing Recommendations

1. **Rankings Endpoint**: Test with various event codes to ensure proper data transformation
2. **Alliances**: Verify alliance captain and pick assignments are correct
3. **Match Descriptions**: Test with 2, 4, and 8 alliance tournaments
4. **Score Transformation**: Verify all scoring details are preserved and bonuses are surfaced
5. **Time Fields**: Check that missing time fields are populated for played matches

---

## API Documentation

All endpoints include:
- XML documentation comments for Swagger
- Proper response type attributes
- Redis caching (24 hours for alliances/rankings, 5-10 minutes for dynamic data)
- Appropriate HTTP status codes (200 OK, 204 No Content)

---

## Files Modified

### Models
- `Models/TBAModels.cs` - Added TBA alliance, ranking, and status models
- `Models/FRCApiModels.cs` - Updated Alliance record to support nullable Round2
- `Models/MatchScoresModels.cs` - **NEW** - Complete match scoring models
- `Models/HybridScheduleModels.cs` - Updated to use MatchScore instead of raw objects

### Controllers
- `Controllers/FRCApiController.cs` - Major enhancements:
  - Added `/offseason/rankings/{eventCode}` endpoint
  - Fixed `/offseason/events` endpoint
  - Enhanced `/offseason/schedule/hybrid/{eventCode}` with match descriptions and time handling
  - Updated `/offseason/alliances/{eventCode}` with full transformation
  - Added comprehensive score breakdown transformation methods

---

## Summary

These changes provide a robust, maintainable foundation for supporting offseason FRC events through TBA integration. The season-agnostic design ensures minimal updates are needed year-over-year, while the comprehensive transformations make the data easy to consume on the client side.

