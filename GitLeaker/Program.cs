using AspNet.Security.OAuth.GitHub;
using GitLeaker.Data;
using GitLeaker.Data.Migrations;
using GitLeaker.Middlewares;
using GitLeaker.Repositories;
using GitLeaker.Repositories.Interfaces;
using GitLeaker.Services;
using GitLeaker.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpContextAccessor();

// ── DB layer ────────────────────────────────────────────
builder.Services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();
builder.Services.AddScoped<IScanRepository, ScanRepository>();

builder.Services.AddScoped<IEntropyService, EntropyService>();
builder.Services.AddScoped<IGitService, GitService>();
builder.Services.AddScoped<IPatternService, PatternService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IScannerService, ScannerService>();
builder.Services.AddScoped<ITokenService, TokenService>();

builder.Services.AddHttpClient("github", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = GitHubAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.Cookie.Name        = "GitLeaker.Session";
    options.Cookie.HttpOnly    = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite    = SameSiteMode.Lax;
    options.ExpireTimeSpan     = TimeSpan.FromDays(7);
    options.SlidingExpiration  = true;

    // API return 401 - front will handle what next ????
    options.Events.OnRedirectToLogin = ctx =>
    {
        ctx.Response.StatusCode = 401;
        return Task.CompletedTask;
    };
})
.AddGitHub(options =>
{
    options.ClientId     = builder.Configuration["GitHub:ClientId"]!;
    options.ClientSecret = builder.Configuration["GitHub:ClientSecret"]!;
    options.CallbackPath = builder.Configuration["GitHub:CallbackPath"]; 
    options.SaveTokens   = true; 
    options.Scope.Add("repo");

    // some properties from the retrieved user
    options.ClaimActions.MapJsonKey("urn:github:login",  "login");
    options.ClaimActions.MapJsonKey("urn:github:avatar", "avatar_url");
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
        policy.WithOrigins("http://localhost:5075", "http://localhost:3000")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});


var app = builder.Build();

// ── Run DB migrations ──────────────────────── 
await MigrationRunner.RunAsync(
    builder.Environment.ContentRootPath,
    app.Configuration.GetConnectionString("DefaultConnection")!,
    app.Logger);

app.UseMiddleware<ExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger(); 
    app.UseSwaggerUI();

}

app.UseCors("AllowFrontend");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
 
app.Run();