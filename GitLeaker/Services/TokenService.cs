using GitLeaker.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;

namespace GitLeaker.Services;

public class TokenService : ITokenService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TokenService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<string?> GetGitHubTokenAsync()
    {
        var ctx = _httpContextAccessor.HttpContext;
        if (ctx == null) return null;
        return await ctx.GetTokenAsync("access_token");
    }
}