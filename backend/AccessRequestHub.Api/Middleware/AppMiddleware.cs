using System.Text.Json;
using AccessRequestHub.Api.DTOs;
using AccessRequestHub.Api.Exceptions;
using AccessRequestHub.Api.Models;
using AccessRequestHub.Api.Services;

namespace AccessRequestHub.Api.Middleware;

public static class HttpContextExtensions
{
    private const string UserItemKey = "CurrentUser";

    public static void SetCurrentUser(this HttpContext context, User user)
    {
        context.Items[UserItemKey] = user;
    }

    public static User? GetCurrentUser(this HttpContext context)
    {
        return context.Items.TryGetValue(UserItemKey, out var user) ? user as User : null;
    }
}

public class SimulatedAuthMiddleware
{
    private readonly RequestDelegate _next;

    public SimulatedAuthMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, AccessRequestService service)
    {
        var userId = context.Request.Headers["x-user-id"].FirstOrDefault();
        var userEmail = context.Request.Headers["x-user-email"].FirstOrDefault();

        User? user = null;
        if (!string.IsNullOrEmpty(userId))
        {
            user = await service.GetUserByIdAsync(userId);
        }
        else if (!string.IsNullOrEmpty(userEmail))
        {
            user = await service.GetUserByEmailAsync(userEmail);
        }

        if (user != null)
        {
            context.SetCurrentUser(user);
        }

        await _next(context);
    }
}

public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (DomainException ex)
        {
            _logger.LogWarning(ex, "Domain exception: {Code} - {Message}", ex.Code, ex.Message);
            context.Response.StatusCode = ex.StatusCode;
            context.Response.ContentType = "application/json";

            var response = new ErrorResponseDto
            {
                Error = new ErrorDetail
                {
                    Code = ex.Code,
                    Message = ex.Message
                }
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOpts));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled server exception");
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";

            var response = new ErrorResponseDto
            {
                Error = new ErrorDetail
                {
                    Code = "INTERNAL_SERVER_ERROR",
                    Message = "An unexpected error occurred"
                }
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOpts));
        }
    }
}
