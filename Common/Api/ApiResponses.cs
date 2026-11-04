using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Common.Api;

public static class ApiResponses
{
    private static ObjectResult Build<T>(
        T? data,
        string status,
        int statusCode,
        string message,
        IDictionary<string, string[]>? errors = null)
    {
        return new ObjectResult(
            new ApiResponse<T>(
                status,
                statusCode,
                message,
                data,
                errors
            ))
        {
            StatusCode = statusCode
        };
    }

    // Success responses

    public static ObjectResult Success<T>(
        T? data,
        string message = "Request processed successfully",
        int statusCode = StatusCodes.Status200OK)
    {
        return Build(
            data,
            ApiResponseStatus.Success,
            statusCode,
            message
        );
    }

    public static ObjectResult Success(
        string message = "Request processed successfully")
    {
        return Success<object?>(null, message);
    }

    public static ObjectResult Created<T>(
        T? data,
        string message = "Resource created successfully")
    {
        return Success(data, message, StatusCodes.Status201Created);
    }

    public static ObjectResult Accepted<T>(
        T? data,
        string message = "Request accepted for processing")
    {
        return Success(data, message, StatusCodes.Status202Accepted);
    }

    // Error responses

    public static ObjectResult ValidationFailed(
        IDictionary<string, string[]> errors,
        string message = "Validation failed")
    {
        return Build<object?>(
            null,
            ApiResponseStatus.Error,
            StatusCodes.Status422UnprocessableEntity,
            message,
            errors
        );
    }

    public static ObjectResult BadRequest(
        string message = "Bad request")
    {
        return Error(StatusCodes.Status400BadRequest, message);
    }

    public static ObjectResult Unauthenticated(
        string message = "Authentication required")
    {
        return Error(StatusCodes.Status401Unauthorized, message);
    }

    public static ObjectResult Forbidden(
        string message = "Access forbidden")
    {
        return Error(StatusCodes.Status403Forbidden, message);
    }

    public static ObjectResult NotFound(
        string message = "Resource not found")
    {
        return Error(StatusCodes.Status404NotFound, message);
    }

    public static ObjectResult Conflict(
        string message = "Resource conflict")
    {
        return Error(StatusCodes.Status409Conflict, message);
    }

    public static ObjectResult TooManyRequests(
        string message = "Too many requests")
    {
        return Error(StatusCodes.Status429TooManyRequests, message);
    }

    public static ObjectResult InternalServerError(
        string message = "Internal server error")
    {
        return Error(
            StatusCodes.Status500InternalServerError,
            message
        );
    }

    public static ObjectResult ServiceUnavailable(
        string message = "Service temporarily unavailable")
    {
        return Error(
            StatusCodes.Status503ServiceUnavailable,
            message
        );
    }

    private static ObjectResult Error(int statusCode, string message)
    {
        return Build<object?>(
            null,
            ApiResponseStatus.Error,
            statusCode,
            message
        );
    }
}