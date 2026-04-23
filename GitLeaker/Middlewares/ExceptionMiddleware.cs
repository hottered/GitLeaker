using System.Text.Json;

namespace GitLeaker.Middlewares;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(
        RequestDelegate next,
        ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ArgumentException ex)
        {
            await HandleException(context, ex, 400);
        }
        catch (UnauthorizedAccessException ex)
        {
            await HandleException(context, ex, 401);
        }
        catch (DirectoryNotFoundException ex)
        {
            await HandleException(context, ex, 404);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");

            await HandleException(
                context,
                new Exception("Internal server error"),
                500);
        }
    }

    private async Task HandleException(
        HttpContext context,
        Exception ex,
        int statusCode)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;

        var response = new
        {
            success = false,
            message = ex.Message,
            status = statusCode
        };

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(response));
    }
}