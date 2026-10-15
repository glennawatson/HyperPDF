// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Drawing;

namespace HyperPdfLibrary.Rendering;

/// <summary>
/// The recorded pictures of one page at one optional content version. Each picture is recorded once; threads that need it
/// while another records wait for it. The pictures are disposed when the entry has left the cache and no render uses it.
/// Content for printing is kept apart, because optional content follows print usage then.
/// </summary>
[DebuggerDisplay("PagePictures: page {PageIndex} version {Version}")]
internal sealed class PagePictures
{
    /// <summary>The bit of <see cref="_usage"/> set once the entry has left the cache; the bits below it count the renders using the entry.</summary>
    private const int RetiredBit = 1 << 30;

    /// <summary>Guards recording and the recorded pictures. It is held for a whole slice of recording.</summary>
    private readonly Lock _gate = new();

    /// <summary>The page content.</summary>
    private IPdfRenderPicture? _content;

    /// <summary>The page content for printing.</summary>
    private IPdfRenderPicture? _printContent;

    /// <summary>The annotation appearances for viewing.</summary>
    private IPdfRenderPicture? _annotations;

    /// <summary>The annotation appearances for printing.</summary>
    private IPdfRenderPicture? _printAnnotations;

    /// <summary>A content recording paused part way, or null.</summary>
    private ProgressiveRecording? _recording;

    /// <summary>The renders using the entry, with <see cref="RetiredBit"/> set once it has left the cache; changed only by interlocked operations.</summary>
    private int _usage;

    /// <summary>The memory the recorded pictures hold for themselves, without the pixels of the images they drew.</summary>
    private long _heldBytes;

    /// <summary>The images the recorded pictures drew.</summary>
    private ImageWeight[] _images = [];

    /// <summary>The pictures recorded.</summary>
    private int _recordings;

    /// <summary>Initializes a new instance of the <see cref="PagePictures"/> class.</summary>
    /// <param name="pageIndex">The page index.</param>
    /// <param name="version">The optional content version.</param>
    internal PagePictures(int pageIndex, int version)
        : this(pageIndex, version, false)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="PagePictures"/> class.</summary>
    /// <param name="pageIndex">The page index.</param>
    /// <param name="version">The optional content version.</param>
    /// <param name="usesIntent">Whether the pictures are recorded with device colours converted through the output intent.</param>
    internal PagePictures(int pageIndex, int version, bool usesIntent)
        : this(pageIndex, version, usesIntent, int.MaxValue, float.PositiveInfinity)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="PagePictures"/> class for one display zoom band.</summary>
    /// <param name="pageIndex">The page index.</param>
    /// <param name="version">The layer version.</param>
    /// <param name="usesIntent">Whether output intent colours are used.</param>
    /// <param name="scaleBand">The logarithmic zoom band.</param>
    /// <param name="upperScale">The highest scale served by the band.</param>
    internal PagePictures(int pageIndex, int version, bool usesIntent, int scaleBand, float upperScale)
        : this(pageIndex, version, usesIntent, scaleBand, upperScale, 0)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="PagePictures"/> class for one display zoom band and layer zoom class.</summary>
    /// <param name="pageIndex">The page index.</param>
    /// <param name="version">The layer version.</param>
    /// <param name="usesIntent">Whether output intent colours are used.</param>
    /// <param name="scaleBand">The logarithmic zoom band.</param>
    /// <param name="upperScale">The highest scale served by the band.</param>
    /// <param name="zoomClass">The layer zoom class, which decides layers with zoom limits.</param>
    internal PagePictures(int pageIndex, int version, bool usesIntent, int scaleBand, float upperScale, int zoomClass)
    {
        PageIndex = pageIndex;
        Version = version;
        UsesIntent = usesIntent;
        ScaleBand = scaleBand;
        UpperScale = upperScale;
        ZoomClass = zoomClass;
    }

    /// <summary>Gets the page-to-device matrix, without the tile offset, the pictures were recorded for; null for pictures that serve a zoom band.</summary>
    internal Matrix3x2? Device { get; init; }

    /// <summary>Gets the layer zoom class the pictures were recorded for.</summary>
    internal int ZoomClass { get; }

    /// <summary>Gets the zoom band this picture was recorded for.</summary>
    internal int ScaleBand { get; }

    /// <summary>Gets the upper scale of the band.</summary>
    internal float UpperScale { get; }

    /// <summary>Gets a value indicating whether the pictures use the output intent conversion.</summary>
    internal bool UsesIntent { get; }

    /// <summary>Gets the page index.</summary>
    internal int PageIndex { get; }

    /// <summary>Gets the optional content version the pictures were recorded at.</summary>
    internal int Version { get; }

    /// <summary>
    /// Gets the memory the recorded pictures hold for themselves, which grows as content and annotations are recorded. It
    /// leaves out the images the pictures drew, which pages can share; <see cref="Images"/> lists them.
    /// </summary>
    internal long HeldBytes => Volatile.Read(ref _heldBytes);

    /// <summary>Gets the images the recorded pictures drew; an image drawn by several pictures is listed for each.</summary>
    internal ImageWeight[] Images => Volatile.Read(ref _images);

    /// <summary>Gets how many pictures have been recorded, which changes whenever <see cref="HeldBytes"/> or <see cref="Images"/> can.</summary>
    internal int Recordings => Volatile.Read(ref _recordings);

    /// <summary>Determines whether the entry holds the pictures a render needs.</summary>
    /// <param name="pageIndex">The page index.</param>
    /// <param name="version">The optional content version.</param>
    /// <param name="usesIntent">Whether the pictures use the output intent conversion.</param>
    /// <param name="scaleBand">The zoom band, or the marker band of an exact page matrix.</param>
    /// <param name="zoomClass">The layer zoom class.</param>
    /// <param name="device">The exact page-to-device matrix, or null for a zoom band.</param>
    /// <returns><see langword="true"/> when the pictures fit.</returns>
    internal bool Serves(int pageIndex, int version, bool usesIntent, int scaleBand, int zoomClass, Matrix3x2? device) =>
        PageIndex == pageIndex && Version == version && UsesIntent == usesIntent && ScaleBand == scaleBand && ZoomClass == zoomClass && Device == device;

    /// <summary>Marks the entry as in use.</summary>
    internal void Acquire() => _ = Interlocked.Increment(ref _usage);

    /// <summary>Marks the end of a use, disposing the pictures if the entry has left the cache.</summary>
    internal void Release()
    {
        if (Interlocked.Decrement(ref _usage) == RetiredBit)
        {
            DisposePictures();
        }
    }

    /// <summary>Marks the entry as removed from the cache.</summary>
    internal void Retire()
    {
        // A recording holds a use, so an entry that is recording is disposed by the Release that ends the use.
        if (Interlocked.Or(ref _usage, RetiredBit) == 0)
        {
            DisposePictures();
        }
    }

    /// <summary>Gets the page content, recording it on first use; a paused progressive recording is finished.</summary>
    /// <param name="cache">The document's caches.</param>
    /// <param name="page">The page.</param>
    /// <param name="printing">Whether optional content follows print usage.</param>
    /// <returns>The picture.</returns>
    internal IPdfRenderPicture GetContent(PdfRenderCache cache, PdfPage page, bool printing)
    {
        lock (_gate)
        {
            if ((printing ? _printContent : _content) is { } existing)
            {
                return existing;
            }

            if (_recording is { } partial && partial.Printing == printing)
            {
                _ = ContinueLocked(cache, page, printing, CancellationToken.None);
                return (printing ? _printContent : _content)!;
            }

            var recorded = PageRecorder.RecordContent(cache, page, printing, new(UpperScale, ZoomClass) { Device = Device }, out var bytes, out var images);
            AddHeld(bytes, images);
            if (printing)
            {
                _printContent = recorded;
            }
            else
            {
                _content = recorded;
            }

            return recorded;
        }
    }

    /// <summary>Records more of the page content, stopping when paused or cancelled.</summary>
    /// <param name="cache">The document's caches.</param>
    /// <param name="page">The page.</param>
    /// <param name="printing">Whether optional content follows print usage.</param>
    /// <param name="shouldPause">Asked between slices; returning <see langword="true"/> pauses.</param>
    /// <param name="cancellationToken">Cancels the recording between slices.</param>
    /// <returns>The status; <see cref="PdfRenderStatus.Done"/> once the content is recorded.</returns>
    internal PdfRenderStatus ContinueContent(PdfRenderCache cache, PdfPage page, bool printing, Func<bool>? shouldPause, CancellationToken cancellationToken)
    {
        while (true)
        {
            PdfRenderStatus status;
            lock (_gate)
            {
                status = (printing ? _printContent : _content) is not null
                    ? PdfRenderStatus.Done
                    : StepLocked(cache, page, printing, cancellationToken);
            }

            // The callback runs between slices with no lock held, so it may call back into the renderer.
            if (status != PdfRenderStatus.Paused || shouldPause?.Invoke() == true)
            {
                return status;
            }
        }
    }

    /// <summary>Gets the annotation appearances, recording them on first use.</summary>
    /// <param name="cache">The document's caches.</param>
    /// <param name="page">The page.</param>
    /// <param name="printing">Whether the annotations are for printing.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The picture.</returns>
    internal IPdfRenderPicture GetAnnotations(PdfRenderCache cache, PdfPage page, bool printing, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if ((printing ? _printAnnotations : _annotations) is { } existing)
            {
                return existing;
            }

            var recorded = PageRecorder.RecordAnnotations(cache, page, printing, new(UpperScale, ZoomClass) { Device = Device }, out var bytes, out var images, cancellationToken);
            AddHeld(bytes, images);
            if (printing)
            {
                _printAnnotations = recorded;
            }
            else
            {
                _annotations = recorded;
            }

            return recorded;
        }
    }

    /// <summary>Adds what a recorded picture holds. The caller holds the lock.</summary>
    /// <param name="bytes">The picture's operations, without the images it drew.</param>
    /// <param name="images">The distinct images the picture drew.</param>
    private void AddHeld(long bytes, ImageWeight[] images)
    {
        if (images.Length > 0)
        {
            // Readers take the array without the lock, so it is replaced rather than grown in place.
            Volatile.Write(ref _images, _images.Length == 0 ? images : [.. _images, .. images]);
        }

        Volatile.Write(ref _heldBytes, _heldBytes + bytes);
        _ = Interlocked.Increment(ref _recordings);
    }

    /// <summary>Runs the progressive recording to its end. The caller holds the lock.</summary>
    /// <param name="cache">The document's caches.</param>
    /// <param name="page">The page.</param>
    /// <param name="printing">Whether optional content follows print usage.</param>
    /// <param name="cancellationToken">Cancels the recording between slices.</param>
    /// <returns>The status.</returns>
    private PdfRenderStatus ContinueLocked(PdfRenderCache cache, PdfPage page, bool printing, CancellationToken cancellationToken)
    {
        PdfRenderStatus status;
        do
        {
            status = StepLocked(cache, page, printing, cancellationToken);
        }
        while (status == PdfRenderStatus.Paused);

        return status;
    }

    /// <summary>Starts the progressive recording or runs one slice of it. The caller holds the lock.</summary>
    /// <param name="cache">The document's caches.</param>
    /// <param name="page">The page.</param>
    /// <param name="printing">Whether optional content follows print usage.</param>
    /// <param name="cancellationToken">Cancels the recording between slices.</param>
    /// <returns><see cref="PdfRenderStatus.Paused"/> while operators remain.</returns>
    private PdfRenderStatus StepLocked(PdfRenderCache cache, PdfPage page, bool printing, CancellationToken cancellationToken)
    {
        if (_recording is { } stale && stale.Printing != printing)
        {
            stale.Dispose();
            _recording = null;
        }

        _recording ??= ProgressiveRecording.Start(cache, page, printing, new(UpperScale, ZoomClass) { Device = Device }, cancellationToken);
        PdfRenderStatus status;
        IPdfRenderPicture? picture;
        try
        {
            status = _recording.Step(cancellationToken, out picture);
        }
        catch (OperationCanceledException)
        {
            // A token that fired inside an operator leaves the recording part way through it, so it cannot resume.
            _recording.Dispose();
            _recording = null;
            throw;
        }

        if (status == PdfRenderStatus.Paused)
        {
            return status;
        }

        if (status != PdfRenderStatus.Done)
        {
            // A cancelled recording is discarded and counts as nothing recorded.
            _recording.Dispose();
            _recording = null;
            return status;
        }

        AddHeld(_recording.Bytes, _recording.Images);
        _recording.Dispose();
        _recording = null;
        if (printing)
        {
            _printContent = picture;
        }
        else
        {
            _content = picture;
        }

        return status;
    }

    /// <summary>Disposes the pictures of an entry that is retired and idle.</summary>
    private void DisposePictures()
    {
        lock (_gate)
        {
            _content?.Dispose();
            _printContent?.Dispose();
            _annotations?.Dispose();
            _printAnnotations?.Dispose();
            _recording?.Dispose();
            _content = null;
            _printContent = null;
            _annotations = null;
            _printAnnotations = null;
            _recording = null;
            Volatile.Write(ref _heldBytes, 0);
            Volatile.Write(ref _images, []);
        }
    }
}
