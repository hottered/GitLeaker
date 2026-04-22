namespace GitLeaker.Services.Interfaces;

public interface ITokenService
{
    Task<string?> GetGitHubTokenAsync();
}