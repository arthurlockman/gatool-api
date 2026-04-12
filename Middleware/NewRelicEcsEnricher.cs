using GAToolAPI.Services;

namespace GAToolAPI.Middleware;

public class NewRelicEcsEnricher(RequestDelegate next, EcsTaskMetadata metadata)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (metadata.IsRunningOnEcs)
        {
            var agent = NewRelic.Api.Agent.NewRelic.GetAgent();
            var txn = agent.CurrentTransaction;
            if (metadata.TaskId != null) txn.AddCustomAttribute("ecs.task.id", metadata.TaskId);
            if (metadata.TaskArn != null) txn.AddCustomAttribute("ecs.task.arn", metadata.TaskArn);
            if (metadata.ClusterName != null) txn.AddCustomAttribute("ecs.cluster", metadata.ClusterName);
            if (metadata.Family != null) txn.AddCustomAttribute("ecs.task.family", metadata.Family);
            if (metadata.Revision != null) txn.AddCustomAttribute("ecs.task.revision", metadata.Revision);
            if (metadata.AvailabilityZone != null) txn.AddCustomAttribute("ecs.availability_zone", metadata.AvailabilityZone);
            if (metadata.LaunchType != null) txn.AddCustomAttribute("ecs.launch_type", metadata.LaunchType);
        }

        await next(context);
    }
}
