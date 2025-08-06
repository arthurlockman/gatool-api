using JetBrains.Annotations;
using System.Reflection;

namespace GAToolAPI.Models;

[UsedImplicitly]
public record VersionResponse(
    string Version,
    string? Sha,
    string BuildDate,
    string Environment);

public static class VersionInfo
{
    public static VersionResponse GetVersionInfo(string environment)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version?.ToString() ?? "unknown";

        // Get git SHA from assembly metadata (set during build)
        var gitSha = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? null;

        // Get build date from assembly
        var buildDate = File.GetCreationTime(assembly.Location).ToString("yyyy-MM-dd HH:mm:ss UTC");

        return new VersionResponse(version, gitSha, buildDate, environment);
    }
}

