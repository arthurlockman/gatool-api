namespace GAToolAPI.Helpers;

/// <summary>
/// FTC season runs fall through spring; the season year is the start year (e.g. 2025 = fall 2025–spring 2026).
/// Use this when differentiating current vs prior seasons for caching or other logic.
/// </summary>
public static class FtcSeasonHelper
{
    /// <summary>
    /// Returns the current FTC season year. In Feb 2026 the current season is still 2025.
    /// </summary>
    public static int GetCurrentSeasonYear()
    {
        var now = DateTime.Now;
        return now.Month >= 9 ? now.Year : now.Year - 1;
    }
}
