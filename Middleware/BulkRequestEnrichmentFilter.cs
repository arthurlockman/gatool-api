using GAToolAPI.Models;
using Microsoft.AspNetCore.Mvc.Filters;
using Serilog;

namespace GAToolAPI.Middleware;

/// <summary>
/// Action filter that enriches the Serilog request log and the New Relic transaction
/// with the contents of any <see cref="TeamQueryRequest"/> body argument. This is what
/// makes bulk <c>query*</c> endpoint payloads searchable in New Relic Logs (via the
/// Serilog request-completed event) and in APM (via custom transaction attributes).
/// </summary>
public class BulkRequestEnrichmentFilter(IDiagnosticContext diagnosticContext) : IActionFilter
{
    private const int MaxLoggedTeams = 1000;

    public void OnActionExecuting(ActionExecutingContext context)
    {
        foreach (var arg in context.ActionArguments.Values)
        {
            if (arg is not TeamQueryRequest req) continue;

            var teams = req.Teams ?? [];
            var teamCount = teams.Count;

            // Cap the array we serialize to avoid blowing up log line size on
            // pathological inputs. The count is always recorded faithfully.
            var loggedTeams = teamCount > MaxLoggedTeams
                ? teams.Take(MaxLoggedTeams).ToList()
                : teams;

            // Surface in Serilog request log (-> New Relic Logs)
            diagnosticContext.Set("BulkTeamCount", teamCount);
            diagnosticContext.Set("BulkTeams", loggedTeams, destructureObjects: true);
            if (teamCount > MaxLoggedTeams) diagnosticContext.Set("BulkTeamsTruncated", true);

            // Surface as custom attributes on the NR APM transaction so they're
            // filterable in NRQL: SELECT * FROM Transaction WHERE bulk.team_count > 100
            try
            {
                var txn = NewRelic.Api.Agent.NewRelic.GetAgent().CurrentTransaction;
                txn.AddCustomAttribute("bulk.team_count", teamCount);
                txn.AddCustomAttribute("bulk.teams", string.Join(",", loggedTeams));
            }
            catch
            {
                // NR agent unavailable (e.g. local dev) — Serilog enrichment still applies.
            }

            break;
        }
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
}
