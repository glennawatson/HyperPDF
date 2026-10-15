// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Drawing;

namespace HyperPdfLibrary.Rendering;

/// <summary>
/// Renders tiles of a document's pages through its selected backend. Each page is recorded once, in viewer space, and every
/// tile replays it, so after the first render of a page a tile costs one replay and no managed allocation. The pictures
/// are kept least recently used first, bounded by the memory they hold (<see cref="PdfRenderOptions.PictureCacheBytes"/>).
/// Safe to call from many threads at once.
/// </summary>
[DebuggerDisplay("PdfPageRenderer: {_entries.Count} pages cached")]
public sealed partial class PdfPageRenderer : IDisposable
{
    /// <summary>The most pages whose pictures are kept, whatever their size, so finding a page stays cheap.</summary>
    private const int CachedPages = 32;

    /// <summary>The mask of the quarter turn count.</summary>
    private const int TurnMask = 3;

    /// <summary>A quarter turn clockwise.</summary>
    private const int QuarterTurn = 1;

    /// <summary>A half turn.</summary>
    private const int HalfTurn = 2;

    /// <summary>The lowest zoom exponent retained as a separate picture band.</summary>
    private const int MinimumScaleBand = -16;

    /// <summary>The highest zoom exponent retained as a separate picture band.</summary>
    private const int MaximumScaleBand = 16;

    /// <summary>The scale band of pictures recorded for one exact page matrix, which lies outside the zoom exponents.</summary>
    private const int ExactScaleBand = int.MinValue;

    /// <summary>The scale multiplier between adjacent picture bands.</summary>
    private const int ScaleStep = 2;

    /// <summary>Guards the picture cache.</summary>
    private readonly Lock _gate = new();

    /// <summary>The cached pictures, least recently used first.</summary>
    private readonly List<PagePictures> _entries = [];

    /// <summary>The document.</summary>
    private readonly PdfDocument _document;

    /// <summary>The cache entry shared with renderers of this document that have equal image and drawing settings.</summary>
    private readonly PdfRenderCacheEntry _cacheEntry;

    /// <summary>The limits the renderer was made with, applied to the output intent caches too.</summary>
    private readonly PdfRenderOptions _options;

    /// <summary>The output-intent cache entry acquired on first use for this renderer's options.</summary>
    private PdfRenderCacheEntry? _intentEntry;

    /// <summary>The ids of the images already added up while measuring the pictures; scratch space for the lock holder, made when a picture first lists an image.</summary>
    private HashSet<uint>? _counted;

    /// <summary>Whether the renderer has been disposed.</summary>
    private bool _disposed;

    /// <summary>Whether the document knows the renderer, so it can release the renderer's pictures on dispose; 0 or 1.</summary>
    private int _registered;

    /// <summary>Initializes a new instance of the <see cref="PdfPageRenderer"/> class.</summary>
    /// <param name="document">The document to render.</param>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    public PdfPageRenderer(PdfDocument document)
        : this(document, PdfRenderOptions.Default)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="PdfPageRenderer"/> class with cache limits for its options.</summary>
    /// <param name="document">The document to render.</param>
    /// <param name="options">The limits and switches for this renderer and its compatible shared caches.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The image cache or picture cache limit is negative.</exception>
    public PdfPageRenderer(PdfDocument document, PdfRenderOptions options)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfNegative(options.PictureCacheBytes, nameof(options));
        _document = document;
        _cacheEntry = PdfDocumentRendering.GetRendererCache(document, options);
        _options = options;
    }

    /// <summary>Gets this renderer's selected normal cache for diagnostics and tests.</summary>
    internal PdfRenderCache Cache => _cacheEntry.Cache;

    /// <summary>Gets the number of pages whose pictures are kept.</summary>
    internal int PictureCount
    {
        get
        {
            lock (_gate)
            {
                return _entries.Count;
            }
        }
    }

    /// <summary>
    /// Gets the memory the kept pictures hold: their operations and the pixels of the images they drew. An image drawn by
    /// several kept pages is counted once.
    /// </summary>
    internal long PictureBytes
    {
        get
        {
            lock (_gate)
            {
                return MeasureLocked(0);
            }
        }
    }

    /// <summary>Gets the size of a whole page image: the rotated physical page size (user space size times <c>/UserUnit</c>) times the scale, rounded up.</summary>
    /// <param name="page">The page.</param>
    /// <param name="quarterTurns">The extra clockwise rotation in quarter turns.</param>
    /// <param name="scale">Device pixels per point of physical size.</param>
    /// <param name="width">Receives the width in pixels.</param>
    /// <param name="height">Receives the height in pixels.</param>
    /// <exception cref="ArgumentNullException"><paramref name="page"/> is <see langword="null"/>.</exception>
    public static void GetPixelSize(PdfPage page, int quarterTurns, float scale, out int width, out int height)
    {
        ArgumentNullException.ThrowIfNull(page);
        scale = EffectiveScale(page, scale);
        var odd = (quarterTurns & QuarterTurn) != 0;
        width = Math.Max(1, (int)MathF.Ceiling((odd ? page.Height : page.Width) * scale));
        height = Math.Max(1, (int)MathF.Ceiling((odd ? page.Width : page.Height) * scale));
    }

    /// <summary>Renders part of a page into BGRA premultiplied pixels.</summary>
    /// <param name="request">Which part of which page to render.</param>
    /// <param name="target">The pixel buffer to fill.</param>
    /// <returns><see langword="false"/> when the page does not exist or the target is invalid.</returns>
    /// <exception cref="ObjectDisposedException">The renderer or its document has been disposed, including while the tile was being drawn.</exception>
    /// <remarks>The target is drawn directly; a drawing exception can leave partial pixels.</remarks>
    public bool Render(in PdfTileRequest request, PdfTileTarget target)
    {
        ObjectDisposedException.ThrowIf(_disposed || _document.IsDisposed, this);
        if ((uint)request.PageIndex >= (uint)_document.PageCount || !target.IsValid)
        {
            return false;
        }

        var page = PdfDocumentPages.GetPage(_document, request.PageIndex);
        var cache = CacheFor(request.Flags);
        var entry = Acquire(page, request, cache);
        try
        {
            var recorded = entry.Recordings;
            var drawn = Draw(_document.DrawingServices.Backend.GetDrawingSession(), entry, cache, page, request, target);
            if (drawn)
            {
                TrimIfRecorded(entry, recorded);
            }

            return drawn;
        }
        finally
        {
            entry.Release();
        }
    }

    /// <summary>Submits a page replay into a target owned by the caller.</summary>
    /// <param name="request">The page region and rendering flags.</param>
    /// <param name="target">The target kept alive through GPU completion and presentation.</param>
    /// <returns>Whether drawing was submitted.</returns>
    public bool RenderToTarget(in PdfTileRequest request, IPdfRenderTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        ObjectDisposedException.ThrowIf(_disposed || _document.IsDisposed, this);
        if ((uint)request.PageIndex >= (uint)_document.PageCount || !target.IsValid)
        {
            return false;
        }

        var page = PdfDocumentPages.GetPage(_document, request.PageIndex);
        var cache = CacheFor(request.Flags);
        var entry = Acquire(page, request, cache);
        try
        {
            var recorded = entry.Recordings;
            var drawn = Draw(_document.DrawingServices.Backend.GetDrawingSession(), entry, cache, page, request, target);
            if (drawn)
            {
                TrimIfRecorded(entry, recorded);
            }

            return drawn;
        }
        finally
        {
            entry.Release();
        }
    }

    /// <summary>Records page content and requested annotations without creating output pixels.</summary>
    /// <param name="request">The page and rendering flags to prepare.</param>
    /// <param name="cancellationToken">Cancels recording between operators and during long operations.</param>
    /// <returns>Whether the requested page exists.</returns>
    public bool Prepare(in PdfTileRequest request, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed || _document.IsDisposed, this);
        if ((uint)request.PageIndex >= (uint)_document.PageCount)
        {
            return false;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var page = PdfDocumentPages.GetPage(_document, request.PageIndex);
        var cache = CacheFor(request.Flags);
        var entry = Acquire(page, request, cache);
        try
        {
            var recorded = entry.Recordings;
            var printing = (request.Flags & PdfRenderFlags.Printing) != 0;
            var status = entry.ContinueContent(cache, page, printing, null, cancellationToken);
            if (status != PdfRenderStatus.Done)
            {
                TrimIfRecorded(entry, recorded);
                return false;
            }

            if ((request.Flags & PdfRenderFlags.Annotations) != 0)
            {
                _ = entry.GetAnnotations(cache, page, printing, cancellationToken);
            }

            TrimIfRecorded(entry, recorded);
            cancellationToken.ThrowIfCancellationRequested();
            return true;
        }
        finally
        {
            entry.Release();
        }
    }

    /// <summary>
    /// Renders part of a page progressively, a slice of operators at a time. While the page is not yet
    /// recorded, its content is recorded a slice of operators at a time, asking <paramref name="shouldPause"/> after
    /// each slice; when it pauses, call again with the same request to continue. The target is drawn only when the
    /// status is <see cref="PdfRenderStatus.Done"/>. Once a page is recorded this costs the same as a normal render.
    /// </summary>
    /// <param name="request">Which part of which page to render.</param>
    /// <param name="target">The pixel buffer to fill.</param>
    /// <param name="shouldPause">Asked between slices; return <see langword="true"/> to pause. Null records to the end.</param>
    /// <param name="cancellationToken">Cancels and discards the recording between slices.</param>
    /// <returns>The status.</returns>
    /// <exception cref="ObjectDisposedException">The renderer has been disposed.</exception>
    /// <remarks>The target is drawn directly when ready; a drawing exception can leave partial pixels.</remarks>
    public PdfRenderStatus RenderProgressive(in PdfTileRequest request, PdfTileTarget target, Func<bool>? shouldPause, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed || _document.IsDisposed, this);
        if ((uint)request.PageIndex >= (uint)_document.PageCount || !target.IsValid)
        {
            return PdfRenderStatus.Failed;
        }

        var page = PdfDocumentPages.GetPage(_document, request.PageIndex);
        var cache = CacheFor(request.Flags);
        var entry = Acquire(page, request, cache);
        try
        {
            var recorded = entry.Recordings;
            var status = entry.ContinueContent(cache, page, (request.Flags & PdfRenderFlags.Printing) != 0, shouldPause, cancellationToken);
            if (status != PdfRenderStatus.Done)
            {
                return status;
            }

            if ((request.Flags & PdfRenderFlags.Annotations) != 0)
            {
                _ = entry.GetAnnotations(cache, page, (request.Flags & PdfRenderFlags.Printing) != 0, cancellationToken);
            }

            var drawn = Draw(_document.DrawingServices.Backend.GetDrawingSession(), entry, cache, page, request, target);
            if (drawn)
            {
                TrimIfRecorded(entry, recorded);
            }

            return drawn ? PdfRenderStatus.Done : PdfRenderStatus.Failed;
        }
        finally
        {
            entry.Release();
        }
    }

    /// <summary>Records in slices, then submits a page replay into a caller-owned target.</summary>
    /// <param name="request">The page region and rendering flags.</param>
    /// <param name="target">The target kept alive through GPU completion and presentation.</param>
    /// <param name="shouldPause">Returns true between slices to pause recording.</param>
    /// <param name="cancellationToken">Cancels recording between slices.</param>
    /// <returns>The recording and drawing status.</returns>
    public PdfRenderStatus RenderProgressiveToTarget(in PdfTileRequest request, IPdfRenderTarget target, Func<bool>? shouldPause, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        ObjectDisposedException.ThrowIf(_disposed || _document.IsDisposed, this);
        if ((uint)request.PageIndex >= (uint)_document.PageCount || !target.IsValid)
        {
            return PdfRenderStatus.Failed;
        }

        var page = PdfDocumentPages.GetPage(_document, request.PageIndex);
        var cache = CacheFor(request.Flags);
        var entry = Acquire(page, request, cache);
        try
        {
            var recorded = entry.Recordings;
            var status = entry.ContinueContent(cache, page, (request.Flags & PdfRenderFlags.Printing) != 0, shouldPause, cancellationToken);
            if (status != PdfRenderStatus.Done)
            {
                return status;
            }

            if ((request.Flags & PdfRenderFlags.Annotations) != 0)
            {
                _ = entry.GetAnnotations(cache, page, (request.Flags & PdfRenderFlags.Printing) != 0, cancellationToken);
            }

            var drawn = Draw(_document.DrawingServices.Backend.GetDrawingSession(), entry, cache, page, request, target);
            if (drawn)
            {
                TrimIfRecorded(entry, recorded);
            }

            return drawn ? PdfRenderStatus.Done : PdfRenderStatus.Failed;
        }
        finally
        {
            entry.Release();
        }
    }

    /// <summary>Releases the cached pictures.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            foreach (var entry in _entries)
            {
                entry.Retire();
            }

            _entries.Clear();
            PdfDocumentRendering.ReleaseRendererCache(_document, _cacheEntry);
            if (_intentEntry is { } intent)
            {
                PdfDocumentRendering.ReleaseRendererCache(_document, intent);
            }
        }
    }

    /// <summary>Gets the device pixels per user space unit: the scale per point of physical size times the page's <c>/UserUnit</c> (ISO 32000-2 7.7.3.3).</summary>
    /// <param name="page">The page.</param>
    /// <param name="scale">The requested scale, in device pixels per point of physical size.</param>
    /// <returns>The pixels per user space unit.</returns>
    internal static float EffectiveScale(PdfPage page, float scale) => scale * page.UserUnit;

    /// <summary>Computes the matrix from viewer space to the target's pixels.</summary>
    /// <param name="page">The page.</param>
    /// <param name="request">The tile request.</param>
    /// <returns>The matrix.</returns>
    internal static Matrix3x2 GetMatrix(PdfPage page, in PdfTileRequest request)
    {
        var w = page.Width;
        var h = page.Height;
        var quarterTurns = request.QuarterTurns & TurnMask;
        var turn = quarterTurns switch
        {
            QuarterTurn => new Matrix3x2(0, 1, -1, 0, h, 0),
            HalfTurn => new Matrix3x2(-1, 0, 0, -1, w, h),
            TurnMask => new Matrix3x2(0, -1, 1, 0, 0, w),
            _ => Matrix3x2.Identity,
        };
        GetPixelSize(page, quarterTurns, request.Scale, out var pixelWidth, out var pixelHeight);
        var odd = (quarterTurns & QuarterTurn) != 0;
        return turn
            * Matrix3x2.CreateScale(pixelWidth / (odd ? h : w), pixelHeight / (odd ? w : h))
            * Matrix3x2.CreateTranslation(-request.OffsetX, -request.OffsetY);
    }

    /// <summary>Clears the canvas and replays the page onto it.</summary>
    /// <param name="session">The drawing session.</param>
    /// <param name="entry">The page's pictures.</param>
    /// <param name="cache">The caches the pictures are recorded with.</param>
    /// <param name="page">The page.</param>
    /// <param name="request">The tile request.</param>
    /// <param name="target">The target.</param>
    /// <returns>Whether the target was drawn.</returns>
    private static bool Draw(IPdfDrawingSession session, PagePictures entry, PdfRenderCache cache, PdfPage page, in PdfTileRequest request, PdfTileTarget target)
    {
        var printing = (request.Flags & PdfRenderFlags.Printing) != 0;
        var content = entry.GetContent(cache, page, printing);
        var annotations = (request.Flags & PdfRenderFlags.Annotations) != 0 ? entry.GetAnnotations(cache, page, printing) : null;
        return session.DrawPage(content, annotations, GetMatrix(page, request), target, (request.Flags & PdfRenderFlags.Grayscale) != 0);
    }

    /// <summary>Replays recorded content on a non-CPU target.</summary>
    /// <param name="session">The drawing session.</param>
    /// <param name="entry">The page pictures.</param>
    /// <param name="cache">The recording caches.</param>
    /// <param name="page">The page.</param>
    /// <param name="request">The tile request.</param>
    /// <param name="target">The owned target.</param>
    /// <returns>Whether drawing was submitted.</returns>
    private static bool Draw(IPdfDrawingSession session, PagePictures entry, PdfRenderCache cache, PdfPage page, in PdfTileRequest request, IPdfRenderTarget target)
    {
        var printing = (request.Flags & PdfRenderFlags.Printing) != 0;
        var content = entry.GetContent(cache, page, printing);
        var annotations = (request.Flags & PdfRenderFlags.Annotations) != 0 ? entry.GetAnnotations(cache, page, printing) : null;
        return session.DrawPage(content, annotations, GetMatrix(page, request), target, (request.Flags & PdfRenderFlags.Grayscale) != 0);
    }

    /// <summary>Rounds a display scale upward so one picture serves its entire power-of-two zoom band.</summary>
    /// <param name="scale">Device pixels per page point.</param>
    /// <param name="upperScale">Receives the highest scale served by the band.</param>
    /// <returns>The zoom band, or the full-resolution band for invalid or very large scales.</returns>
    private static int GetScaleBand(float scale, out float upperScale)
    {
        if (!float.IsFinite(scale) || scale <= 0 || scale > MathF.ScaleB(1, MaximumScaleBand))
        {
            upperScale = float.PositiveInfinity;
            return int.MaxValue;
        }

        var exponent = Math.Clamp(MathF.ILogB(scale), MinimumScaleBand, MaximumScaleBand);
        upperScale = MathF.ScaleB(1, exponent);
        if (upperScale < scale)
        {
            upperScale *= ScaleStep;
            exponent++;
        }

        return exponent;
    }

    /// <summary>Gets the band pictures are kept under: the zoom band of a scale, or the marker band of pictures recorded for one exact page matrix.</summary>
    /// <param name="scale">Device pixels per page point.</param>
    /// <param name="device">The page-to-device matrix without the tile offset, or null for a zoom band.</param>
    /// <param name="upperScale">Receives the highest scale the pictures serve; for an exact matrix, the scale itself.</param>
    /// <returns>The band.</returns>
    private static int GetPictureBand(float scale, Matrix3x2? device, out float upperScale)
    {
        if (device is null)
        {
            return GetScaleBand(scale, out upperScale);
        }

        upperScale = scale;
        return ExactScaleBand;
    }

    /// <summary>
    /// Chooses the caches a render records with: the output intent's when the flag asks for it, or when the document claims
    /// PDF/A and has a usable intent (ISO 19005 asks a reader to convert device colours through it).
    /// </summary>
    /// <param name="flags">The render flags.</param>
    /// <returns>The cache selected by this renderer's options and the output-intent flag.</returns>
    private PdfRenderCache CacheFor(PdfRenderFlags flags)
    {
        if ((flags & PdfRenderFlags.OutputIntent) == 0 && !PdfDocumentOutputIntentRendering.ClaimsPdfA(_document))
        {
            return Cache;
        }

        if (Volatile.Read(ref _intentEntry) is { } configured)
        {
            return configured.Cache;
        }

        var documentIntent = PdfDocumentOutputIntentRendering.GetOutputIntentRenderCache(_document);
        if (documentIntent is null)
        {
            return Cache;
        }

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed || _document.IsDisposed, this);
            if (_intentEntry is not { } entry)
            {
                entry = PdfDocumentRendering.GetOutputIntentRendererCache(_document, _options, documentIntent);
                Volatile.Write(ref _intentEntry, entry);
            }

            return entry.Cache;
        }
    }

    /// <summary>Finds or creates the entry for a tile's page, recorded for the exact page matrix when the quality settings need it.</summary>
    /// <param name="page">The page.</param>
    /// <param name="request">The tile request.</param>
    /// <param name="cache">The caches the tile records with.</param>
    /// <returns>The entry; release it when done.</returns>
    private PagePictures Acquire(PdfPage page, in PdfTileRequest request, PdfRenderCache cache)
    {
        Matrix3x2? device = cache.Quality.NeedsDeviceMatrix ? GetMatrix(page, request with { OffsetX = 0, OffsetY = 0 }) : null;
        return Acquire(request.PageIndex, !ReferenceEquals(cache, Cache), EffectiveScale(page, request.Scale), device);
    }

    /// <summary>Finds or creates the entry for a page at the current optional content version, and marks it in use.</summary>
    /// <param name="pageIndex">The page index.</param>
    /// <param name="usesIntent">Whether the pictures convert device colours through the output intent.</param>
    /// <param name="scale">Device pixels per page point.</param>
    /// <param name="device">The page-to-device matrix without the tile offset when pictures are recorded for it; otherwise null for a zoom band.</param>
    /// <returns>The entry; release it when done.</returns>
    private PagePictures Acquire(int pageIndex, bool usesIntent, float scale, Matrix3x2? device)
    {
        if (Volatile.Read(ref _registered) == 0 && Interlocked.Exchange(ref _registered, 1) == 0)
        {
            // Registered on first use rather than in the constructor, so the document never sees a half-built renderer.
            PdfDocumentRendering.RegisterRenderer(_document, this);
        }

        var layers = PdfDocumentLayers.GetOptionalContent(_document);
        var version = layers.Version;
        var zoomClass = layers.GetZoomClass(scale);
        var scaleBand = GetPictureBand(scale, device, out var upperScale);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            for (var i = 0; i < _entries.Count; i++)
            {
                var existing = _entries[i];
                if (!existing.Serves(pageIndex, version, usesIntent, scaleBand, zoomClass, device))
                {
                    continue;
                }

                _entries.RemoveAt(i);
                _entries.Add(existing);
                existing.Acquire();
                return existing;
            }

            var created = new PagePictures(pageIndex, version, usesIntent, scaleBand, upperScale, zoomClass) { Device = device };
            _entries.Add(created);
            created.Acquire();
            EvictLocked();
            return created;
        }
    }

    /// <summary>Drops old pages when this render recorded pictures that pushed the pictures over their memory limit.</summary>
    /// <param name="entry">The page the render drew.</param>
    /// <param name="recordedBefore">The pictures the page had recorded before the render.</param>
    private void TrimIfRecorded(PagePictures entry, int recordedBefore)
    {
        if (entry.Recordings == recordedBefore)
        {
            return;
        }

        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            // The newest page stays even when it alone is over the limit. An image leaves the total only when the last kept
            // page that drew it is dropped, so each step measures the pages that remain.
            var dropped = 0;
            while (_entries.Count - dropped > 1 && MeasureLocked(dropped) > _options.PictureCacheBytes)
            {
                _entries[dropped].Retire();
                dropped++;
            }

            _entries.RemoveRange(0, dropped);
        }
    }

    /// <summary>Adds up the memory the pictures of the kept pages hold, counting each image once however many pages drew it. The caller holds the lock.</summary>
    /// <param name="first">The index of the first kept page to include; earlier pages are being dropped.</param>
    /// <returns>The bytes held.</returns>
    private long MeasureLocked(int first)
    {
        _counted?.Clear();
        var total = 0L;
        for (var i = first; i < _entries.Count; i++)
        {
            var picture = _entries[i];
            total += picture.HeldBytes;
            foreach (var (id, bytes) in picture.Images)
            {
                if ((_counted ??= []).Add(id))
                {
                    total += bytes;
                }
            }
        }

        return total;
    }

    /// <summary>Removes the least recently used entries beyond the limit and any stale versions of the newest page.</summary>
    private void EvictLocked()
    {
        var last = _entries.Count - 1;
        var newest = _entries[last];
        for (var i = last - 1; i >= 0; i--)
        {
            if (_entries[i].PageIndex != newest.PageIndex || _entries[i].ScaleBand != newest.ScaleBand || _entries[i].Device != newest.Device)
            {
                continue;
            }

            _entries[i].Retire();
            _entries.RemoveAt(i);
        }

        while (_entries.Count > CachedPages)
        {
            _entries[0].Retire();
            _entries.RemoveAt(0);
        }
    }
}
