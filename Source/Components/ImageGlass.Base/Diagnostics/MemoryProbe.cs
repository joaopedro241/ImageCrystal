// ImageCrystal modifications by xenon, 2026-10-18; see NOTICE.ImageCrystal.txt.
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace ImageGlass.Base.Diagnostics;

/// <summary>
/// Local, opt-in stage snapshots. Weak references never extend image lifetimes.
/// Pixel estimates describe representations, not additive physical allocations.
/// </summary>
public static class MemoryProbe
{
    private static readonly object Gate = new();
    private static readonly ConditionalWeakTable<object, Resource> Identities = new();
    private static readonly List<Resource> Resources = [];
    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private static readonly string? DirectoryPath = Environment.GetEnvironmentVariable("IMAGECRYSTAL_MEMORY_LOG");
    private static readonly bool CollectAfterSettling = Environment.GetEnvironmentVariable("IMAGECRYSTAL_MEMORY_GC") == "1";
    private static string? _logPath;
    private static long _sequence, _resourceId, _phaseId;
    private static int _settleGeneration;

    public static bool Enabled => !string.IsNullOrWhiteSpace(DirectoryPath);
    public static string? LogPath => _logPath;

    private sealed class Resource(object target, string role, long? pixelBytes, string storage)
    {
        public long Id { get; } = Interlocked.Increment(ref _resourceId);
        public WeakReference<object> Target { get; } = new(target);
        public string Type { get; } = target.GetType().FullName ?? target.GetType().Name;
        public HashSet<string> Roles { get; } = [role];
        public long? PixelBytes { get; } = pixelBytes;
        public string Storage { get; } = storage;
        public bool Disposed { get; set; }
    }

    public static void Track(object? target, string role, long? estimatedPixelBytes = null, string storage = "CPU source; backing storage not measured")
    {
        if (!Enabled || target == null) return;
        lock (Gate)
        {
            if (Identities.TryGetValue(target, out var existing)) existing.Roles.Add(role);
            else
            {
                Resources.RemoveAll(r => !r.Target.TryGetTarget(out _));
                var resource = new Resource(target, role, estimatedPixelBytes, storage);
                Identities.Add(target, resource);
                Resources.Add(resource);
            }
        }
    }

    public static void MarkDisposed(object? target)
    {
        if (!Enabled || target == null) return;
        lock (Gate)
            if (Identities.TryGetValue(target, out var resource)) resource.Disposed = true;
    }

    public static void Capture(string stage, object? details = null)
    {
        if (!Enabled) return;
        try
        {
            lock (Gate)
            {
                if (_logPath == null)
                {
                    Directory.CreateDirectory(DirectoryPath!);
                    _logPath = Path.Combine(DirectoryPath!, $"memory-{Environment.ProcessId}-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.jsonl");
                }
                using var process = Process.GetCurrentProcess();
                var gc = GC.GetGCMemoryInfo();
                var row = new
                {
                    schema = 1, sequence = ++_sequence, utc = DateTime.UtcNow,
                    elapsedMs = Clock.ElapsedMilliseconds, pid = Environment.ProcessId,
                    thread = Environment.CurrentManagedThreadId, stage, details,
                    workingSetBytes = process.WorkingSet64,
                    privateCommitBytes = process.PrivateMemorySize64,
                    peakWorkingSetBytes = process.PeakWorkingSet64,
                    managedOccupiedBytes = GC.GetTotalMemory(false),
                    managedAllocatedTotalBytes = GC.GetTotalAllocatedBytes(false),
                    gcCommittedBytes = gc.TotalCommittedBytes,
                    lastGcHeapBytes = gc.HeapSizeBytes, lastGcFragmentedBytes = gc.FragmentedBytes,
                    gen0Collections = GC.CollectionCount(0), gen1Collections = GC.CollectionCount(1), gen2Collections = GC.CollectionCount(2),
                    trackedResources = Resources.Where(r => r.Target.TryGetTarget(out _)).Select(r => new
                    {
                        id = r.Id, type = r.Type, roles = r.Roles.ToArray(), disposed = r.Disposed,
                        estimatedPixelBytes = r.PixelBytes, storage = r.Storage,
                    }).ToArray(),
                };
                File.AppendAllText(_logPath, JsonSerializer.Serialize(row) + Environment.NewLine);
            }
        }
        catch (Exception ex)
        {
            // A diagnostic failure must not interrupt viewing, loading or disposal.
            Debug.WriteLine($"Memory probe: {ex.Message}");
        }
    }

    public static IDisposable Begin(string stage, object? details = null)
        => Enabled ? new Phase(stage, details) : EmptyPhase.Instance;

    private sealed class EmptyPhase : IDisposable
    {
        public static readonly EmptyPhase Instance = new();
        public void Dispose() { }
    }

    private sealed class Phase : IDisposable
    {
        private readonly string _stage;
        private readonly object? _details;
        private readonly long _id = Interlocked.Increment(ref _phaseId);
        private readonly long _started = Clock.ElapsedMilliseconds;
        private bool _disposed;
        public Phase(string stage, object? details)
        {
            _stage = stage;
            _details = details;
            Capture(stage + ".before", new { phaseId = _id, context = details });
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Capture(_stage + ".after", new { phaseId = _id, durationMs = Clock.ElapsedMilliseconds - _started, context = _details });
        }
    }

    /// <summary>Two finite snapshots per completed load; superseded loads are ignored.</summary>
    public static void ScheduleSettledSnapshots(Action snapshotUiState)
    {
        if (!Enabled) return;
        var generation = Interlocked.Increment(ref _settleGeneration);
        _ = TakeSettledSnapshotsAsync(generation, snapshotUiState);
    }

    private static async Task TakeSettledSnapshotsAsync(int generation, Action snapshotUiState)
    {
        try
        {
            await Task.Delay(2000).ConfigureAwait(false);
            if (generation != Volatile.Read(ref _settleGeneration)) return;
            Capture("settled.2s");
            snapshotUiState();
            await Task.Delay(4000).ConfigureAwait(false);
            if (generation != Volatile.Read(ref _settleGeneration)) return;
            Capture("settled.6s");
            if (CollectAfterSettling)
            {
                // Explicit diagnostic experiment only. Never used by ordinary launches.
                Capture("diagnostic-gc.before");
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: false);
                GC.WaitForPendingFinalizers();
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: false);
                Capture("diagnostic-gc.after");
            }
            snapshotUiState();
        }
        catch (Exception ex) { Debug.WriteLine($"Settled memory probe: {ex.Message}"); }
    }
}
