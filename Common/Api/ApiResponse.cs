namespace Crm.Api.Common.Api;

public class ApiResponse<T>(
    string status,
    int statusCode,
    string message,
    T? data = default,
    IDictionary<string, string[]>? errors = null)
{
    public string Status { get; set; } = status;

    public int StatusCode { get; set; } = statusCode;
    public string Message { get; set; } = message;

    public T? Data { get; set; } = data;

    public IDictionary<string, string[]>? Errors { get; set; } = errors;
}