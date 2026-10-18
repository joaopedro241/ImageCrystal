// ImageCrystal modifications by xenon, 2026-10-18; see NOTICE.ImageCrystal.txt.
/*
Derived from ImageGlass v9, Copyright (C) 2010 - 2026 DUONG DIEU PHAP.
ImageCrystal modifications, 2026. GPL-3.0-or-later; see LICENSE.
*/
using ImageGlass.Base.Photoing.Codecs;

namespace ImageGlass.Base.Services;

/// <summary>A folder snapshot with optional, finite neighbour preloading.</summary>
public class NeighbourImageCache : IDisposable
{
    private readonly object _sync = new();
    private readonly SemaphoreSlim _decodeGate = new(1, 1);
    private readonly List<IgPhoto> _images = [];
    private CancellationTokenSource? _preloadCancellation;
    private Task _preloadTask = Task.CompletedTask;
    private IgPhoto? _current;
    private bool _disposed;
    private int _maxQueue;

    public CodecReadOptions ReadOptions { get; set; } = new();
    public List<string> DistinctDirs { get; set; } = [];
    public HashSet<string> SingleFrameFormats { get; set; } = [];
    public int MaxImageDimensionToCache { get; set; }
    public float MaxFileSizeInMbToCache { get; set; }
    public event EventHandler<EventArgs>? OnFinishLoadingImage;
    public int Length { get { lock (_sync) return _images.Count; } }
    public List<string> FilePaths { get { lock (_sync) return _images.Select(x => x.FilePath).ToList(); } }

    /// <summary>Neighbours per direction. Zero cancels preloading and keeps only the current image.</summary>
    public int MaxQueue
    {
        get { lock (_sync) return _maxQueue; }
        set
        {
            lock (_sync)
            {
                _maxQueue = Math.Clamp(value, 0, 10);
                CancelPreloading();
                if (_maxQueue == 0)
                    foreach (var image in _images.Where(x => x != _current)) image.Dispose();
            }
        }
    }

    public NeighbourImageCache(IEnumerable<string>? list = null)
    {
        if (list != null) Add(list);
    }

    private void CancelPreloading() => _preloadCancellation?.Cancel();
    private IgPhoto? At(int index) => index >= 0 && index < _images.Count ? _images[index] : null;

    public async Task<IgPhoto?> GetAsync(int index, bool useCache = true, CancellationTokenSource? tokenSrc = null)
    {
        var token = tokenSrc?.Token ?? CancellationToken.None;
        token.ThrowIfCancellationRequested();
        IgPhoto? image;
        lock (_sync)
        {
            if (_disposed) return null;
            image = At(index);
            if (image == null) return null;
            CancelPreloading();
            _current = image;
            if (_maxQueue == 0)
                foreach (var previous in _images.Where(x => x != image)) previous.Dispose();
        }
        await _decodeGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            lock (_sync)
            {
                if (_disposed || !_images.Contains(image)) return null;
                if (!useCache) image.Unload();
            }
            if (!image.IsDone || (image.Error == null && image.ImgData.IsImageNull))
                await image.LoadAsync(ReadOptions with
                {
                    FirstFrameOnly = SingleFrameFormats.Contains(image.Extension),
                }, tokenSrc).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            lock (_sync)
            {
                if (_disposed || !_images.Contains(image)) { image.Dispose(); return null; }
            }
        }
        finally { _decodeGate.Release(); }
        // Awaiting the actual decode replaces the old IsDone polling loop.
        StartCaching(IndexOf(image.FilePath), false);
        OnFinishLoadingImage?.Invoke(this, EventArgs.Empty);
        return image;
    }

    public void StartCaching(int index, bool includeCurrentIndex)
    {
        lock (_sync)
        {
            CancelPreloading();
            if (_disposed || At(index) is not IgPhoto current) return;
            var selected = new HashSet<IgPhoto> { current };
            var queue = new List<IgPhoto>();
            if (includeCurrentIndex && _maxQueue > 0) queue.Add(current);
            for (var distance = 1; distance <= Math.Min(_maxQueue, _images.Count - 1); distance++)
            {
                foreach (var i in new[] { (index + distance) % _images.Count, (index - distance + _images.Count) % _images.Count })
                    if (selected.Add(_images[i])) queue.Add(_images[i]);
            }
            foreach (var image in _images.Where(x => !selected.Contains(x))) image.Dispose();
            // With zero neighbours there is no task, metadata ping, timer, or hidden current-image job.
            if (queue.Count == 0) { _preloadTask = Task.CompletedTask; return; }
            var cancellation = new CancellationTokenSource();
            _preloadCancellation = cancellation;
            var options = ReadOptions;
            var singleFrameFormats = SingleFrameFormats.ToHashSet(StringComparer.OrdinalIgnoreCase);
            _preloadTask = Task.Run(async () =>
            {
                try
                {
                    foreach (var image in queue)
                    {
                        await _decodeGate.WaitAsync(cancellation.Token).ConfigureAwait(false);
                        try
                        {
                            cancellation.Token.ThrowIfCancellationRequested();
                            lock (_sync) { if (_disposed || !_images.Contains(image)) continue; }
                            if (image.IsDone) continue;
                            if (image != current && (MaxImageDimensionToCache > 0 || MaxFileSizeInMbToCache > 0))
                            {
                                var metadata = image.Metadata ?? PhotoCodec.LoadMetadata(image.FilePath);
                                if ((MaxImageDimensionToCache > 0 && Math.Max(metadata.RenderedWidth, metadata.RenderedHeight) > MaxImageDimensionToCache)
                                    || (MaxFileSizeInMbToCache > 0 && metadata.FileSize / 1048576f > MaxFileSizeInMbToCache)) continue;
                            }
                            await image.LoadAsync(options with
                            {
                                FirstFrameOnly = singleFrameFormats.Contains(image.Extension),
                            }, cancellation).ConfigureAwait(false);
                            lock (_sync)
                                if (_disposed || !_images.Contains(image) || cancellation.IsCancellationRequested) image.Dispose();
                        }
                        // Neighbour failures do not prevent the foreground image from opening.
                        catch (Exception ex) when (ex is not OperationCanceledException) { }
                        finally { _decodeGate.Release(); }
                    }
                }
                catch (OperationCanceledException) { }
                finally
                {
                    lock (_sync)
                    {
                        if (_preloadCancellation == cancellation) _preloadCancellation = null;
                        cancellation.Dispose();
                    }
                }
            });
        }
    }

    /// <summary>Completes when the currently scheduled finite preload batch finishes.</summary>
    public Task WaitForPreloadingAsync() { lock (_sync) return _preloadTask; }
    public void CancelLoading(int index) { lock (_sync) At(index)?.CancelLoading(); }
    public IgMetadata? GetMetadata(int index, int? frameIndex)
    {
        lock (_sync)
        {
            var image = At(index);
            if (image == null) return null;
            if (image.Metadata == null || image.Metadata.FrameIndex != frameIndex)
                image.Metadata = PhotoCodec.LoadMetadata(image.FilePath, ReadOptions with { FrameIndex = frameIndex });
            return image.Metadata;
        }
    }
    public void Add(string path, int index = -1) => Add([path], index);
    public void Add(IEnumerable<string> paths, int index = -1)
    {
        lock (_sync)
        {
            CancelPreloading();
            var items = paths.Select(x => new IgPhoto(x));
            if (index < 0) _images.AddRange(items); else _images.InsertRange(index, items);
        }
    }
    public bool IsCached(int index) { lock (_sync) return At(index)?.ImgData.IsImageNull == false; }
    public string GetFilePath(int index) { lock (_sync) return At(index)?.FilePath ?? string.Empty; }
    public string GetFileExtension(int index) => Path.GetExtension(GetFilePath(index));
    public void SetFileName(int index, string path) { lock (_sync) { CancelPreloading(); if (At(index) is IgPhoto image) image.FilePath = path; } }
    public int IndexOf(string path) { lock (_sync) return _images.FindIndex(x => string.Equals(x.FilePath, path, StringComparison.OrdinalIgnoreCase)); }
    public void Unload(int index) { lock (_sync) At(index)?.Dispose(); }
    public void Remove(int index)
    {
        lock (_sync)
        {
            CancelPreloading();
            if (At(index) is not IgPhoto image) return;
            image.Dispose();
            _images.RemoveAt(index);
            if (_current == image) _current = null;
        }
    }
    public void ClearCache() { lock (_sync) { CancelPreloading(); foreach (var image in _images) image.Dispose(); } }
    public void Clear() { lock (_sync) { ClearCache(); _images.Clear(); _current = null; } }
    public void Reset() => Clear();
    public void UpdateCache()
    {
        lock (_sync)
        {
            ClearCache();
            // Reload the displayed image on demand; optional neighbours follow normal navigation.
        }
    }
    public bool ContainsDirPathOf(string path)
    {
        lock (_sync) return _images.Any(x => string.Equals(Path.GetDirectoryName(x.FilePath), Path.GetDirectoryName(path), StringComparison.OrdinalIgnoreCase));
    }
    public void Dispose() { lock (_sync) { if (_disposed) return; _disposed = true; Clear(); } }
}
