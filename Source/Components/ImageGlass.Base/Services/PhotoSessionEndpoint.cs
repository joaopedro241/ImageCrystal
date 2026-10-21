// ImageCrystal, copyright (C) 2026 xenon. SPDX-License-Identifier: GPL-3.0-or-later
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.IO.Pipes;
using System.Text.Json;

namespace ImageGlass.Base.Services;

public sealed record PhotoSessionSnapshot(string? Path, bool IsLoading = false);
public sealed record PhotoSessionCapture(string[] Paths, string[] Warnings);

/// <summary>A dormant, same-user endpoint. No UI access, polling or image data.</summary>
public sealed class PhotoSessionEndpoint : IDisposable
{
    private const int MaxResponseBytes = 262144;
    private readonly CancellationTokenSource _stop = new();
    private PhotoSessionSnapshot _snapshot = new(null);
    private int _disposed;
    public Task Completion { get; }
    public string? Error { get; private set; }
    private static string PipeName(int processId) => $"ImageCrystal.PhotoSession.v1.{processId}";

    public PhotoSessionEndpoint() => Completion = ListenAsync(_stop.Token);

    public void Update(string? path, bool isLoading = false) =>
        Volatile.Write(ref _snapshot, new(path, isLoading));

    private async Task ListenAsync(CancellationToken stop)
    {
        try
        {
            while (!stop.IsCancellationRequested)
            {
                using var pipe = new NamedPipeServerStream(PipeName(Environment.ProcessId),
                    PipeDirection.Out, 1, PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(stop).ConfigureAwait(false);
                // The timeout exists only while answering an explicit request.
                using var reply = CancellationTokenSource.CreateLinkedTokenSource(stop);
                reply.CancelAfter(TimeSpan.FromSeconds(1));
                try
                {
                    var data = JsonSerializer.SerializeToUtf8Bytes(Volatile.Read(ref _snapshot));
                    var length = new byte[4];
                    BinaryPrimitives.WriteInt32LittleEndian(length, data.Length);
                    await pipe.WriteAsync(length, reply.Token).ConfigureAwait(false);
                    await pipe.WriteAsync(data, reply.Token).ConfigureAwait(false);
                }
                catch (IOException) { } // Client closed; accept the next request.
                catch (OperationCanceledException) when (!stop.IsCancellationRequested) { }
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Error = ex.Message; // A failed session endpoint must not break image viewing.
        }
    }

    public static async Task<PhotoSessionSnapshot> ReadAsync(int processId,
        CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(1));
        using var pipe = new NamedPipeClientStream(".", PipeName(processId),
            PipeDirection.In, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await pipe.ConnectAsync(timeout.Token).ConfigureAwait(false);
        var length = new byte[4];
        await pipe.ReadExactlyAsync(length, timeout.Token).ConfigureAwait(false);
        var size = BinaryPrimitives.ReadInt32LittleEndian(length);
        if (size is < 1 or > MaxResponseBytes) throw new IOException("Invalid photo-session response size.");
        var data = new byte[size];
        await pipe.ReadExactlyAsync(data, timeout.Token).ConfigureAwait(false);
        var snapshot = JsonSerializer.Deserialize<PhotoSessionSnapshot>(data)
            ?? throw new IOException("Invalid photo-session response.");
        if (snapshot.Path != null && !System.IO.Path.IsPathFullyQualified(snapshot.Path))
            throw new IOException("The photo-session path is not absolute.");
        return snapshot;
    }

    public static int[] GetViewerProcessIds()
    {
        using var current = Process.GetCurrentProcess();
        var ids = new List<int>();
        foreach (var process in Process.GetProcessesByName("ImageCrystal"))
        {
            using (process)
            {
                try
                {
                    if (!process.HasExited && process.SessionId == current.SessionId) ids.Add(process.Id);
                }
                catch (InvalidOperationException) { } // Closed during enumeration.
                catch (System.ComponentModel.Win32Exception) { }
            }
        }
        return ids.ToArray();
    }

    public static async Task<PhotoSessionCapture> CaptureAsync(IEnumerable<int> processIds,
        CancellationToken cancellationToken = default)
    {
        var paths = new System.Collections.Concurrent.ConcurrentBag<string>();
        var warnings = new System.Collections.Concurrent.ConcurrentBag<string>();
        await Parallel.ForEachAsync(processIds.Distinct(), new ParallelOptions
        {
            MaxDegreeOfParallelism = 16, CancellationToken = cancellationToken,
        }, async (id, token) =>
        {
            try
            {
                var snapshot = await ReadAsync(id, token).ConfigureAwait(false);
                if (snapshot.IsLoading) warnings.Add($"ImageCrystal process {id}: image is still loading; not captured.");
                else if (!string.IsNullOrWhiteSpace(snapshot.Path)) paths.Add(snapshot.Path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                or JsonException || (ex is OperationCanceledException && !token.IsCancellationRequested))
            {
                warnings.Add($"ImageCrystal process {id}: unavailable, closed, or running an older build; not captured.");
            }
        }).ConfigureAwait(false);
        return new(paths.Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.CurrentCultureIgnoreCase).ToArray(), warnings.Order().ToArray());
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _stop.Cancel();
        _ = Completion.ContinueWith(_ => _stop.Dispose(), CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }
}
