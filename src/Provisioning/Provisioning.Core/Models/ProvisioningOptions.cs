namespace OpenEdgePlatform.Provisioning.Core.Models;

/// <summary>Configuration for the provisioning worker. Bound from <c>appsettings:Provisioning</c>.</summary>
public sealed class ProvisioningOptions
{
    public const string SectionName = "Provisioning";

    /// <summary>All regions the fleet can place new instances in.</summary>
    public IReadOnlyList<string> AvailableRegions { get; init; } = ["us-east-1", "us-west-2", "eu-west-1"];

    /// <summary>How many regions a single instance is replicated to.</summary>
    public int RegionsPerInstance { get; init; } = 2;

    /// <summary>Maximum endpoints to keep per upstream service name.</summary>
    public int MaxEndpointsPerService { get; init; } = 32;
}
