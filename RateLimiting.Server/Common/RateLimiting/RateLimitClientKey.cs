namespace RateLimiting.Server.Common.RateLimiting;

public static class RateLimitClientKey
{
    private const string ClientIdHeader = "X-Client-Id";

    public static string Resolve(HttpContext httpContext)
    {
        var clientId = httpContext.Request.Headers[ClientIdHeader].ToString();
        if (!string.IsNullOrWhiteSpace(clientId))
        {
            return clientId;
        }

        return httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
    }
}
