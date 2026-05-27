using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OpenEdgePlatform.ServiceBroker.Core.Models;

namespace OpenEdgePlatform.ServiceBroker.Api.Middleware;

/// <summary>
/// Rejects requests that omit the OSB <c>X-Broker-API-Version</c> header or carry an
/// unsupported version. The OSB spec mandates 412 Precondition Failed in those cases.
/// </summary>
public sealed class OsbVersionMiddleware
{
    public const string HeaderName = "X-Broker-API-Version";
    public const string SupportedVersion = "2.17";

    private readonly RequestDelegate _next;
    private readonly ILogger<OsbVersionMiddleware> _logger;

    public OsbVersionMiddleware(RequestDelegate next, ILogger<OsbVersionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/v2"))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        if (!context.Request.Headers.TryGetValue(HeaderName, out var values) || values.Count == 0)
        {
            await WriteErrorAsync(context, "PreconditionFailed", "Missing X-Broker-API-Version header.").ConfigureAwait(false);
            return;
        }

        var version = values.ToString();
        if (!IsCompatible(version))
        {
            _logger.LogWarning("Rejecting request with X-Broker-API-Version={Version}.", version);
            await WriteErrorAsync(context, "PreconditionFailed", $"Unsupported broker API version '{version}'. Expected {SupportedVersion}.").ConfigureAwait(false);
            return;
        }

        await _next(context).ConfigureAwait(false);
    }

    private static bool IsCompatible(string version)
    {
        if (!Version.TryParse(version, out var parsed))
        {
            return false;
        }
        var supported = Version.Parse(SupportedVersion);
        return parsed.Major == supported.Major && parsed >= new Version(supported.Major, 13);
    }

    private static async Task WriteErrorAsync(HttpContext context, string error, string description)
    {
        context.Response.StatusCode = StatusCodes.Status412PreconditionFailed;
        context.Response.ContentType = "application/json";
        var body = JsonSerializer.Serialize(new OsbErrorResponse { Error = error, Description = description });
        await context.Response.WriteAsync(body).ConfigureAwait(false);
    }
}
