using Microsoft.AspNetCore.Mvc;
using OpenEdgePlatform.ServiceBroker.Core.Interfaces;
using OpenEdgePlatform.ServiceBroker.Core.Models;

namespace OpenEdgePlatform.ServiceBroker.Api.Controllers;

/// <summary>OSB v2 service-instance endpoints. Always returns within milliseconds — provisioning is async.</summary>
[ApiController]
[Route("v2/service_instances")]
[Produces("application/json")]
public sealed class ServiceInstancesController : ControllerBase
{
    private readonly IServiceInstanceService _service;
    private readonly ILogger<ServiceInstancesController> _logger;

    public ServiceInstancesController(IServiceInstanceService service, ILogger<ServiceInstancesController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>OSB provision: PUT /v2/service_instances/{instance_id}?accepts_incomplete=true</summary>
    [HttpPut("{instanceId}")]
    public async Task<IActionResult> Provision(
        [FromRoute] string instanceId,
        [FromQuery(Name = "accepts_incomplete")] bool acceptsIncomplete,
        [FromBody] ProvisionRequest request,
        CancellationToken ct)
    {
        if (!acceptsIncomplete)
        {
            return UnprocessableEntity(new OsbErrorResponse
            {
                Error = "AsyncRequired",
                Description = "This service plan requires client support for asynchronous provisioning."
            });
        }

        try
        {
            var result = await _service.ProvisionAsync(instanceId, request, ct).ConfigureAwait(false);
            return result.Outcome switch
            {
                ProvisionOutcome.AcceptedAsync => Accepted(new ProvisionResponse
                {
                    DashboardUrl = result.DashboardUrl,
                    Operation = result.Operation
                }),
                ProvisionOutcome.AlreadyExistsIdentical => Ok(new ProvisionResponse
                {
                    DashboardUrl = $"/dashboard/{instanceId}"
                }),
                ProvisionOutcome.Conflict => Conflict(new OsbErrorResponse
                {
                    Error = "Conflict",
                    Description = "An instance with the same id exists with different parameters."
                }),
                _ => BadRequest()
            };
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Provision validation failure for {InstanceId}.", instanceId);
            return BadRequest(new OsbErrorResponse { Error = "BadRequest", Description = ex.Message });
        }
    }

    /// <summary>OSB deprovision: DELETE /v2/service_instances/{instance_id}?service_id=&plan_id=&accepts_incomplete=true</summary>
    [HttpDelete("{instanceId}")]
    public async Task<IActionResult> Deprovision(
        [FromRoute] string instanceId,
        [FromQuery(Name = "service_id")] string serviceId,
        [FromQuery(Name = "plan_id")] string planId,
        [FromQuery(Name = "accepts_incomplete")] bool acceptsIncomplete,
        CancellationToken ct)
    {
        if (!acceptsIncomplete)
        {
            return UnprocessableEntity(new OsbErrorResponse
            {
                Error = "AsyncRequired",
                Description = "This service plan requires client support for asynchronous deprovisioning."
            });
        }

        var result = await _service.DeprovisionAsync(instanceId, serviceId, planId, ct).ConfigureAwait(false);
        return result.Outcome switch
        {
            ProvisionOutcome.AcceptedAsync => Accepted(new DeprovisionResponse { Operation = result.Operation }),
            ProvisionOutcome.Gone => StatusCode(StatusCodes.Status410Gone, new OsbErrorResponse
            {
                Error = "Gone",
                Description = "Instance does not exist."
            }),
            _ => BadRequest()
        };
    }

    /// <summary>OSB last-operation polling: GET /v2/service_instances/{instance_id}/last_operation</summary>
    [HttpGet("{instanceId}/last_operation")]
    public async Task<IActionResult> LastOperation([FromRoute] string instanceId, CancellationToken ct)
    {
        var response = await _service.GetLastOperationAsync(instanceId, ct).ConfigureAwait(false);
        if (response is null)
        {
            return StatusCode(StatusCodes.Status410Gone, new OsbErrorResponse
            {
                Error = "Gone",
                Description = "Instance does not exist or has been deprovisioned."
            });
        }
        return Ok(response);
    }

    /// <summary>OSB fetch instance: GET /v2/service_instances/{instance_id}</summary>
    [HttpGet("{instanceId}")]
    public async Task<IActionResult> Get([FromRoute] string instanceId, CancellationToken ct)
    {
        var instance = await _service.GetAsync(instanceId, ct).ConfigureAwait(false);
        if (instance is null)
        {
            return NotFound();
        }

        return Ok(new ServiceInstanceResponse
        {
            ServiceId = instance.ServiceId,
            PlanId = instance.PlanId,
            DashboardUrl = $"/dashboard/{instance.InstanceId}",
            Parameters = instance.Parameters,
            Metadata = new Dictionary<string, object>
            {
                ["state"] = instance.State.ToString(),
                ["created_at"] = instance.CreatedAt,
                ["updated_at"] = instance.UpdatedAt
            }
        });
    }
}
