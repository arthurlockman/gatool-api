namespace GAToolAPI.Services;

public interface ISecretProvider
{
    string GetSecret(string name);
    Task<string> GetSecretAsync(string name, CancellationToken cancellationToken = default);
}
