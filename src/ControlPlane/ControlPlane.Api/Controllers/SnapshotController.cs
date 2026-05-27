using Microsoft.AspNetCore.Mvc;
using OpenEdgePlatform.ControlPlane.Core.Interfaces;
using OpenEdgePlatform.ControlPlane.Core.Models;

namespace OpenEdgePlatform.ControlPlane.Api.Controllers;

/// <summary>Operator-facing REST surface for inspecting and force-pushing snapshots.</summary>
[ApiController]
[Route("api")]
[Produces("application/json")]
public sealed class SnapshotController : ControllerBase
{
    private readonly IXdsResourceRepository _repository;
    private readonly IXdsSnapshotPublisher _publisher;

    public SnapshotController(IXdsResourceRepository repository, IXdsSnapshotPublisher publisher)
    {
        _repository = repository;
        _publisher = publisher;
    }

    [HttpGet("snapshots")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var snapshots = await _repository.ListAsync(ct).ConfigureAwait(false);
        return Ok(snapshots);
    }

    [HttpGet("snapshots/{instanceId}")]
    public async Task<IActionResult> Get([FromRoute] string instanceId, CancellationToken ct)
    {
        var snapshot = await _repository.GetByInstanceAsync(instanceId, ct).ConfigureAwait(false);
        return snapshot is null ? NotFound() : Ok(snapshot);
    }

    /// <summary>Re-pushes the current snapshot to all connected proxies in its regions.</summary>
    [HttpPost("snapshots/{instanceId}/push")]
    public async Task<IActionResult> ForcePush([FromRoute] string instanceId, CancellationToken ct)
    {
        var stored = await _repository.GetByInstanceAsync(instanceId, ct).ConfigureAwait(false);
        if (stored is null)
        {
            return NotFound();
        }
        await _publisher.PushAsync(stored.Snapshot, Array.Empty<string>(), ct).ConfigureAwait(false);
        return Accepted(new { instanceId, version = stored.Version });
    }

    [HttpGet("proxies")]
    public IActionResult ListProxies() => Ok(_publisher.ListProxies());
}

[ApiController]
[Route("health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new
    {
        status = "ok",
        service = "control-plane",
        timestamp = DateTimeOffset.UtcNow
    });
}
