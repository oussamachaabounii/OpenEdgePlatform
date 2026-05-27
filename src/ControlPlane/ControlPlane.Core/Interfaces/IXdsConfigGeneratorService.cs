using OpenEdgePlatform.ProxyConfig.Core.Models;
using OpenEdgePlatform.ServiceBroker.Core.Models;

namespace OpenEdgePlatform.ControlPlane.Core.Interfaces;

/// <summary>Translates a worker <see cref="EdgeResourceAllocation"/> into a versioned xDS snapshot.</summary>
public interface IXdsConfigGeneratorService
{
    XdsSnapshot GenerateSnapshot(EdgeResourceAllocation allocation, string version);
}
