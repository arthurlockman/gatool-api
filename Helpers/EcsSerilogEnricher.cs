using GAToolAPI.Services;
using Serilog.Core;
using Serilog.Events;

namespace GAToolAPI.Helpers;

public class EcsSerilogEnricher(EcsTaskMetadata metadata) : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        if (!metadata.IsRunningOnEcs) return;

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ecs.task.id", metadata.TaskId));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ecs.cluster", metadata.ClusterName));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ecs.task.family", metadata.Family));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ecs.task.revision", metadata.Revision));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ecs.availability_zone", metadata.AvailabilityZone));
    }
}
