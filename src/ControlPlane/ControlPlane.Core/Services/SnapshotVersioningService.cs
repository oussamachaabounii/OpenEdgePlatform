namespace OpenEdgePlatform.ControlPlane.Core.Services;

/// <summary>
/// Generates monotonically increasing snapshot versions. Envoy compares version strings as
/// opaque tokens, so any strictly-increasing format works; we use unix-ms.
/// </summary>
public interface ISnapshotVersioning
{
    string Next();
}

public sealed class SnapshotVersioningService : ISnapshotVersioning
{
    private readonly TimeProvider _clock;
    private long _last;

    public SnapshotVersioningService(TimeProvider? clock = null) => _clock = clock ?? TimeProvider.System;

    public string Next()
    {
        while (true)
        {
            var now = _clock.GetUtcNow().ToUnixTimeMilliseconds();
            var current = Interlocked.Read(ref _last);
            var candidate = Math.Max(now, current + 1);
            if (Interlocked.CompareExchange(ref _last, candidate, current) == current)
            {
                return candidate.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
        }
    }
}
