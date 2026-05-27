using FluentAssertions;
using OpenEdgePlatform.ControlPlane.Core.Services;
using Xunit;

namespace OpenEdgePlatform.ControlPlane.Tests.Unit;

public sealed class SnapshotVersioningServiceTests
{
    [Fact]
    public void Next_returns_strictly_increasing_values()
    {
        var sut = new SnapshotVersioningService();
        var values = Enumerable.Range(0, 1000).Select(_ => long.Parse(sut.Next())).ToArray();
        values.Should().BeInAscendingOrder();
        values.Distinct().Should().HaveCount(values.Length);
    }

    [Fact]
    public void Next_is_thread_safe()
    {
        var sut = new SnapshotVersioningService();
        var bag = new System.Collections.Concurrent.ConcurrentBag<string>();
        Parallel.For(0, 5000, _ => bag.Add(sut.Next()));
        bag.Distinct().Should().HaveCount(bag.Count);
    }
}
