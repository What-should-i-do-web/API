namespace WhatShouldIDo.Application.Common;

public enum ProviderStatus
{
    Success,
    RateLimited,
    ApiKeyInvalid,
    Timeout,
    NetworkError,
    NoResults,
    UnknownError
}

public class ProviderResult<T>
{
    public ProviderStatus Status { get; init; }
    public T? Data { get; init; }
    public int Count { get; init; }
    public string? ErrorMessage { get; init; }
    public int? HttpStatusCode { get; init; }
    public string ProviderName { get; init; } = string.Empty;
    public string? SkippedReason { get; init; }

    public bool IsSuccess => Status == ProviderStatus.Success;
    public bool HasResults => Data != null && Count > 0;

    public static ProviderResult<T> Success(T data, int count, string providerName) => new()
    {
        Status = ProviderStatus.Success,
        Data = data,
        Count = count,
        ProviderName = providerName
    };

    public static ProviderResult<T> RateLimited(string providerName, string? reason = null) => new()
    {
        Status = ProviderStatus.RateLimited,
        ProviderName = providerName,
        SkippedReason = reason ?? "Rate limit exceeded",
        Count = 0
    };

    public static ProviderResult<T> ApiKeyInvalid(string providerName, int? httpStatus = null) => new()
    {
        Status = ProviderStatus.ApiKeyInvalid,
        ProviderName = providerName,
        HttpStatusCode = httpStatus,
        SkippedReason = "API key is invalid or missing",
        Count = 0
    };

    public static ProviderResult<T> Timeout(string providerName, string? message = null) => new()
    {
        Status = ProviderStatus.Timeout,
        ProviderName = providerName,
        ErrorMessage = message ?? "Request timed out",
        SkippedReason = "Timeout",
        Count = 0
    };

    public static ProviderResult<T> NetworkError(string providerName, string? message = null) => new()
    {
        Status = ProviderStatus.NetworkError,
        ProviderName = providerName,
        ErrorMessage = message,
        SkippedReason = "Network error",
        Count = 0
    };

    public static ProviderResult<T> NoResults(string providerName) => new()
    {
        Status = ProviderStatus.NoResults,
        ProviderName = providerName,
        Count = 0
    };

    public static ProviderResult<T> Error(string providerName, string? message = null) => new()
    {
        Status = ProviderStatus.UnknownError,
        ProviderName = providerName,
        ErrorMessage = message,
        SkippedReason = "Unknown error",
        Count = 0
    };
}
