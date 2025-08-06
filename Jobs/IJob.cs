namespace GAToolAPI.Jobs;

public interface IJob
{
    string Name { get; }
    Task ExecuteAsync(CancellationToken cancellationToken = default);
}