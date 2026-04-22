using AspNet.Security.OAuth.GitHub;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GitLeaker.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _config;

    public AuthController(IConfiguration config)
    {
        _config = config;
    }

    // Korisnik klikne "Login with GitHub" → frontend ga redirect-uje ovde
    [HttpGet("login")]
    public IActionResult Login()
    {
        if (User.Identity?.IsAuthenticated == true)
            return Ok(new { message = "Vec si ulogovan." });

        var props = new AuthenticationProperties
        {
            // Nakon što GitHub završi, middleware redirect-uje korisnika na frontend
            // RedirectUri  = _config["GitHub:FrontendUrl"],
            RedirectUri  = "/auth/me",
            IsPersistent = true
        };

        // Middleware preuzima kontrolu:
        // 1. Redirect na github.com/login/oauth/authorize
        // 2. GitHub poziva /auth/callback automatski
        // 3. Middleware zameni code za token, sačuva u cookie
        // 4. Redirect na RedirectUri (frontend)
        return Challenge(props, GitHubAuthenticationDefaults.AuthenticationScheme);
    }

    // /auth/callback NE TREBA da pišeš — middleware ga hvata sam
    // CallbackPath = "/auth/callback" u Program.cs je dovoljan

    // Frontend poziva ovo na startup da proveri da li postoji aktivna sesija
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var token    = await HttpContext.GetTokenAsync("access_token");
        var username = User.FindFirst("urn:github:login")?.Value;
        var avatar   = User.FindFirst("urn:github:avatar")?.Value;

        return Ok(new
        {
            username,
            avatar,
            isAuthenticated = true,
            hasToken        = !string.IsNullOrEmpty(token)
        });
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok(new { message = "Uspešno odjavljen." });
    }
}
