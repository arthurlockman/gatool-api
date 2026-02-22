namespace GAToolAPI.Jobs;

public interface IJob
{
    Task ExecuteAsync(CancellationToken cancellationToken = default);
}