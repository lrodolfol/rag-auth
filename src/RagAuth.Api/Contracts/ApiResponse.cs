using Microsoft.AspNetCore.Mvc;

namespace RagAuth.Api.Contracts;

/// <remarks>
/// <c>Error</c> é declarado como <c>object</c> para que o serializador use o tipo real do
/// <see cref="ProblemDetails"/> (por exemplo, <see cref="HttpValidationProblemDetails"/> com <c>errors</c>).
/// Só é preenchido por <see cref="ApiResponse.Failed"/>.
/// </remarks>
public sealed record ApiResponse<TData>(int StatusCode, bool Success, string Message, TData? Data, object? Error);

public static class ApiResponse
{
    public static ApiResponse<TData> Succeeded<TData>(int statusCode, string message, TData data) =>
        new(statusCode, true, message, data, null);

    public static ApiResponse<object> Failed(ProblemDetails problemDetails) =>
        new(
            problemDetails.Status ?? StatusCodes.Status500InternalServerError,
            false,
            problemDetails.Detail ?? problemDetails.Title ?? string.Empty,
            null,
            problemDetails);
}
