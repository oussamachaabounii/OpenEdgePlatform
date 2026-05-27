using FluentAssertions;
using Microsoft.Extensions.Options;
using OpenEdgePlatform.Provisioning.Core.Models;
using OpenEdgePlatform.Provisioning.Core.Services;
using Xunit;

namespace OpenEdgePlatform.Provisioning.Tests.Unit;

public sealed class RoundRobinRegionSelectorTests
{
    private static RoundRobinRegionSelector NewSelector(int perInstance = 2, params string[] regions) =>
        new(Options.Create(new ProvisioningOptions
        {
            AvailableRegions = regions.Length == 0 ? new[] { "a", "b", "c" } : regions,
            RegionsPerInstance = perInstance
        }));

    [Fact]
    public void Rotates_through_regions_round_robin()
    {
        var sut = NewSelector(perInstance: 1);
        var first = sut.SelectRegions();
        var second = sut.SelectRegions();
        var third = sut.SelectRegions();
        var fourth = sut.SelectRegions();

        first[0].Should().Be("a");
        second[0].Should().Be("b");
        third[0].Should().Be("c");
        fourth[0].Should().Be("a");
    }

    [Fact]
    public void Selects_requested_count()
    {
        var sut = NewSelector(perInstance: 2);
        var picked = sut.SelectRegions();
        picked.Should().HaveCount(2);
    }

    [Fact]
    public void Intersects_with_preferred_when_provided()
    {
        var sut = NewSelector(perInstance: 2);
        var picked = sut.SelectRegions(new[] { "a", "c" });
        picked.Should().OnlyContain(r => r == "a" || r == "c");
    }

    [Fact]
    public void Falls_back_to_full_pool_when_preferred_has_no_overlap()
    {
        var sut = NewSelector(perInstance: 1);
        var picked = sut.SelectRegions(new[] { "unknown-region" });
        new[] { "a", "b", "c" }.Should().Contain(picked[0]);
    }

    [Fact]
    public void Caps_at_available_pool_size()
    {
        var sut = NewSelector(perInstance: 10, "only");
        var picked = sut.SelectRegions();
        picked.Should().HaveCount(1);
    }

    [Fact]
    public void Concurrent_callers_get_distinct_starting_points()
    {
        var sut = NewSelector(perInstance: 1);
        var picks = Enumerable.Range(0, 30)
            .AsParallel()
            .Select(_ => sut.SelectRegions()[0])
            .GroupBy(r => r)
            .ToDictionary(g => g.Key, g => g.Count());

        picks.Values.Sum().Should().Be(30);
        picks.Values.Should().AllSatisfy(c => c.Should().BeInRange(8, 12));
    }
}
