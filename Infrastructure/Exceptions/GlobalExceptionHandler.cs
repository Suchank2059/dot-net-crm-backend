using Crm.Api.Common.Api;
using Microsoft.AspNetCore.Diagnostics;

namespace Crm.Api.Infrastructure.Exceptions;

public class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger
) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // Don't attempt to write a response if the client disconnected.
        if (exception is OperationCanceledException &&
            httpContext.RequestAborted.IsCancellationRequested)
        {
            return false;
        }

        logger.LogError(
            exception,
            "An unhandled exception occurred while processing {Method} {Path}",
            httpContext.Request.Method,
            httpContext.Request.Path
        );

        var (statusCode, message) = exception switch
        {
            KeyNotFoundException =>
                (StatusCodes.Status404NotFound, "Resource not found"),

            UnauthorizedAccessException =>
                (StatusCodes.Status403Forbidden, "Access forbidden"),

            ArgumentException =>
                (StatusCodes.Status400BadRequest, "Invalid request"),

            _ =>
                (StatusCodes.Status500InternalServerError,
                 "An internal server error occurred")
        };

        httpContext.Response.StatusCode = statusCode;

        var response = new ApiResponse<object?>(
            ApiResponseStatus.Error,
            statusCode,
            message
        );

        await httpContext.Response.WriteAsJsonAsync(
            response,
            cancellationToken
        );

        return true;
    }
}