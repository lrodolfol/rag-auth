using Microsoft.Net.Http.Headers;

namespace RagAuth.Api.Infrastructure.RagApi;

public sealed class HttpContextRequestTokenAccessor(IHttpContextAccessor httpContextAccessor) : ICurrentRequestTokenAccessor
{
    public string? AuthorizationHeader
    {
        get
        {
            var authorization = httpContextAccessor.HttpContext?.Request.Headers[HeaderNames.Authorization].ToString();
            return string.IsNullOrWhiteSpace(authorization) ? null : authorization;
        }
    }
}
