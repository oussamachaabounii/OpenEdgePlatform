using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using OpenEdgePlatform.ServiceBroker.Api.Middleware;
using Xunit;

namespace OpenEdgePlatform.ServiceBroker.Tests.Unit;

public sealed class OsbVersionMiddlewareTests
{
    [Fact]
    public async Task Returns_412_when_header_missing_on_osb_path()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/v2/service_instances/abc";
        ctx.Response.Body = new MemoryStream();

        var middleware = new OsbVersionMiddleware(_ => Task.CompletedTask, NullLogger<OsbVersionMiddleware>.Instance);
        await middleware.Invoke(ctx);

        ctx.Response.StatusCode.Should().Be(StatusCodes.Status412PreconditionFailed);
    }

    [Fact]
    public async Task Passes_through_when_supported_version_present()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/v2/service_instances/abc";
        ctx.Request.Headers[OsbVersionMiddleware.HeaderName] = "2.17";
        ctx.Response.Body = new MemoryStream();

        var nextCalled = false;
        var middleware = new OsbVersionMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, NullLogger<OsbVersionMiddleware>.Instance);
        await middleware.Invoke(ctx);

        nextCalled.Should().BeTrue();
        ctx.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task Ignores_non_v2_paths()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/health";

        var nextCalled = false;
        var middleware = new OsbVersionMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, NullLogger<OsbVersionMiddleware>.Instance);
        await middleware.Invoke(ctx);

        nextCalled.Should().BeTrue();
    }

    [Theory]
    [InlineData("1.0")]
    [InlineData("3.0")]
    [InlineData("garbage")]
    public async Task Rejects_incompatible_versions(string version)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/v2/service_instances/abc";
        ctx.Request.Headers[OsbVersionMiddleware.HeaderName] = version;
        ctx.Response.Body = new MemoryStream();

        var middleware = new OsbVersionMiddleware(_ => Task.CompletedTask, NullLogger<OsbVersionMiddleware>.Instance);
        await middleware.Invoke(ctx);

        ctx.Response.StatusCode.Should().Be(StatusCodes.Status412PreconditionFailed);
    }
}
