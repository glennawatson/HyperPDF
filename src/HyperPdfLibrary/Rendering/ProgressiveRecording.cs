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
/// Records a page's content a slice of operators at a time, like FPDF_RenderPageBitmap_Start and _Continue, so a large
/// page can be paused or cancelled between slices. The interpreter, the device and the decoded content live here
/// between calls. Not thread-safe; the owning <see cref = "PagePictures"/> serialises access.
/// </summary>
[DebuggerDisplay("ProgressiveRecording: {_position} of {_length} bytes")]
internal sealed class ProgressiveRecording : IDisposable
{
    /// <summary>The top-level operators run between pause checks.</summary>
    internal const int SliceOperators = 256;

    /// <summary>The recording device.</summary>
    private readonly IPictureDevice _device;

    /// <summary>The interpreter, part way through the page.</summary>
    private readonly ContentInterpreter _interpreter;

    /// <summary>The bytes of content.</summary>
    private readonly int _length;

    /// <summary>The decoded content, rented from the shared pool.</summary>
    private byte[]? _content;

    /// <summary>The offset to resume at.</summary>
    private int _position;

    /// <summary>Initializes a new instance of the <see cref = "ProgressiveRecording"/> class.</summary>
    /// <param name = "cache">The document's caches.</param>
    /// <param name = "page">The page.</param>
    /// <param name = "printing">Whether optional content follows print usage.</param>
    /// <param name="imageScale">The upper device scale of the zoom band.</param>
    internal ProgressiveRecording(PdfRenderCache cache, PdfPage page, bool printing, float imageScale = float.PositiveInfinity)
    {
        Printing = printing;
        _device = PageRecorder.Begin(page);
        _interpreter = new(cache, _device, 0, imageScale) { Printing = printing, };
        ContentExecution.BeginPage(_interpreter, page);
        var buffer = default(PooledBuffer);
        try
        {
            ContentExecution.DecodeContents(page, ref buffer);
            _length = buffer.Length;
            _content = ArrayPool<byte>.Shared.Rent(Math.Max(1, _length));
            buffer.WrittenSpan.CopyTo(_content);
        }
        finally
        {
            buffer.Dispose();
        }
    }

    /// <summary>Gets a value indicating whether optional content follows print usage.</summary>
    internal bool Printing { get; }

    /// <summary>Gets the memory the finished picture holds for itself, without its images, set once the recording is done.</summary>
    internal long Bytes { get; private set; }

    /// <summary>Gets the distinct images the finished picture drew, set once the recording is done.</summary>
    internal ImageWeight[] Images { get; private set; } = [];

    /// <inheritdoc/>
    public void Dispose()
    {
        _interpreter.Dispose();
        _device.Dispose();
        if (_content is not { } content)
        {
            return;
        }

        _content = null;
        ArrayPool<byte>.Shared.Return(content);
    }

    /// <summary>Runs slices until the content ends, the token is cancelled or the pause callback asks to stop.</summary>
    /// <param name = "shouldPause">Asked after each slice; returning <see langword="true"/> pauses. Null runs to the end.</param>
    /// <param name = "cancellationToken">Cancels the recording between slices.</param>
    /// <param name = "picture">Receives the finished picture, which the caller owns, when the status is <see cref = "PdfRenderStatus.Done"/>.</param>
    /// <returns>The status.</returns>
    internal PdfRenderStatus Continue(Func<bool>? shouldPause, CancellationToken cancellationToken, out IPdfRenderPicture? picture)
    {
        picture = null;
        while (true)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return PdfRenderStatus.Cancelled;
            }

            if (ContentExecution.RunSlice(_interpreter, _content.AsSpan(0, _length), ref _position, SliceOperators))
            {
                ContentExecution.EndPage(_interpreter);
                picture = PageRecorder.Finish(_device, out var bytes, out var images);
                Bytes = bytes;
                Images = images;
                return PdfRenderStatus.Done;
            }

            if (shouldPause?.Invoke() == true)
            {
                return PdfRenderStatus.Paused;
            }
        }
    }
}
