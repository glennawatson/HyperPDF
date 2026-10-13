// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Buffers;
using System.Diagnostics;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Rendering;

/// <summary>
/// Records a page's content a slice of operators at a time, so a large
/// page can be paused or cancelled between slices. The interpreter, the device and the decoded content live here
/// between calls. Not thread-safe; the owning <see cref = "PagePictures"/> serialises access.
/// </summary>
[DebuggerDisplay("ProgressiveRecording: {_position} of {_length} bytes")]
internal sealed class ProgressiveRecording : IDisposable
{
    /// <summary>The top-level operators run between pause checks.</summary>
    internal const int SliceOperators = 256;

    /// <summary>Idle recordings, kept so a page recorded in slices reuses the object that tracks its progress.</summary>
    private static readonly ReusableSlots<ProgressiveRecording> Idle = new();

    /// <summary>The recording device; null while the recording sits idle.</summary>
    private IPictureDevice _device = null!;

    /// <summary>The interpreter, part way through the page; null while the recording sits idle.</summary>
    private ContentInterpreter _interpreter = null!;

    /// <summary>The bytes of content.</summary>
    private int _length;

    /// <summary>The decoded content, rented from the shared pool.</summary>
    private byte[]? _content;

    /// <summary>The offset to resume at.</summary>
    private int _position;

    /// <summary>One once <see cref="Dispose"/> has run; the interpreter and device go back to their idle slots only once.</summary>
    private int _disposed = 1;

    /// <summary>Gets a value indicating whether optional content follows print usage.</summary>
    internal bool Printing { get; private set; }

    /// <summary>Gets the memory the finished picture holds for itself, without its images, set once the recording is done.</summary>
    internal long Bytes { get; private set; }

    /// <summary>Gets the distinct images the finished picture drew, set once the recording is done.</summary>
    internal ImageWeight[] Images { get; private set; } = [];

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        // A Begin that failed part way leaves the later fields unset.
        _interpreter?.Dispose();
        _device?.Dispose();
        _interpreter = null!;
        _device = null!;
        Bytes = 0;
        Images = [];
        _position = 0;
        _length = 0;
        if (_content is { } content)
        {
            _content = null;
            ArrayPool<byte>.Shared.Return(content);
        }

        _ = Idle.Offer(this);
    }

    /// <summary>Starts recording a page, using an idle recording when there is one.</summary>
    /// <param name = "cache">The document's caches.</param>
    /// <param name = "page">The page.</param>
    /// <param name = "printing">Whether optional content follows print usage.</param>
    /// <param name="imageScale">The upper device scale of the zoom band.</param>
    /// <param name="zoomClass">The layer zoom class of the magnification.</param>
    /// <param name="cancellationToken">Cancels content decoding and recording.</param>
    /// <returns>The recording, which the caller disposes.</returns>
    internal static ProgressiveRecording Start(PdfRenderCache cache, PdfPage page, bool printing, float imageScale, int zoomClass, CancellationToken cancellationToken)
    {
        var recording = Idle.Take() ?? new();
        try
        {
            recording.Begin(cache, page, printing, imageScale, zoomClass, cancellationToken);
        }
        catch
        {
            // Begin rents the device and interpreter before it decodes the content; give them back when it fails.
            recording.Dispose();
            throw;
        }

        return recording;
    }

    /// <summary>Runs one slice of operators.</summary>
    /// <param name = "cancellationToken">Cancels the recording before the slice.</param>
    /// <param name = "picture">Receives the finished picture, which the caller owns, when the status is <see cref = "PdfRenderStatus.Done"/>.</param>
    /// <returns><see cref="PdfRenderStatus.Paused"/> while operators remain, <see cref="PdfRenderStatus.Done"/> at the end of the content, or <see cref="PdfRenderStatus.Cancelled"/>.</returns>
    internal PdfRenderStatus Step(CancellationToken cancellationToken, out IPdfRenderPicture? picture)
    {
        picture = null;
        if (cancellationToken.IsCancellationRequested)
        {
            return PdfRenderStatus.Cancelled;
        }

        _interpreter.SetCancellationToken(cancellationToken);
        if (!ContentExecution.RunSlice(_interpreter, _content.AsSpan(0, _length), ref _position, SliceOperators))
        {
            return PdfRenderStatus.Paused;
        }

        ContentExecution.EndPage(_interpreter);
        picture = PageRecorder.Finish(_device, out var bytes, out var images);
        Bytes = bytes;
        Images = images;
        return PdfRenderStatus.Done;
    }

    /// <summary>Points the recording at a page and decodes its content.</summary>
    /// <param name = "cache">The document's caches.</param>
    /// <param name = "page">The page.</param>
    /// <param name = "printing">Whether optional content follows print usage.</param>
    /// <param name="imageScale">The upper device scale of the zoom band.</param>
    /// <param name="zoomClass">The layer zoom class of the magnification.</param>
    /// <param name="cancellationToken">Cancels content decoding and recording.</param>
    private void Begin(PdfRenderCache cache, PdfPage page, bool printing, float imageScale, int zoomClass, CancellationToken cancellationToken)
    {
        Printing = printing;
        Volatile.Write(ref _disposed, 0);
        _device = PageRecorder.Begin(cache, page);
        _interpreter = ContentInterpreter.Rent(cache, _device, 0, imageScale, cancellationToken);
        _interpreter.Printing = printing;
        _interpreter.ZoomClass = zoomClass;
        ContentExecution.BeginPage(_interpreter, page);
        var buffer = default(PooledBuffer);
        try
        {
            ContentExecution.DecodeContents(page, ref buffer, cancellationToken);
            _length = buffer.Length;
            _content = ArrayPool<byte>.Shared.Rent(Math.Max(1, _length));
            buffer.WrittenSpan.CopyTo(_content);
        }
        finally
        {
            buffer.Dispose();
        }
    }
}
