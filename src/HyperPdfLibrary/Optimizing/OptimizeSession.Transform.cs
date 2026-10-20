// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Optimizing;

/// <content>Re-encoding objects as they are written.</content>
internal sealed partial class OptimizeSession
{
    /// <inheritdoc/>
    public PdfValue Transform(int oldNumber, PdfValue value)
    {
        // The writer may call back on another thread, so each object uses this session's token.
        _cancellationToken.ThrowIfCancellationRequested();
        if (value.AsStream() is not { } stream)
        {
            return TransformFontNames(oldNumber, value);
        }

        if (_images.TryGetValue(oldNumber, out var use) && TransformImage(oldNumber, stream, use) is { } image)
        {
            return PdfValue.FromStream(image);
        }

        if (_fonts is { } fonts && fonts.Subsets.TryGetValue(oldNumber, out var subset))
        {
            return PdfValue.FromStream(subset);
        }

        if (_options.RecompressStreams && StreamRecompressor.TryRecompress(stream, _source.Objects.ScratchPools, _cancellationToken) is { } smaller)
        {
            _report.Measure(PdfOptimizeCategory.Streams, stream.RawLength, smaller.RawLength);
            return PdfValue.FromStream(smaller);
        }

        return value;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Progress(int written, int total, long bytes) => _progress?.Report(new(PdfOptimizePhase.Writing, written, total, bytes));

    /// <summary>Adds the subset tag to the names of a font object whose program was cut down.</summary>
    /// <param name="oldNumber">The object's number in the source.</param>
    /// <param name="value">The object.</param>
    /// <returns>The object with tagged names, or the object itself when it needs none.</returns>
    private PdfValue TransformFontNames(int oldNumber, PdfValue value)
    {
        var renamed = _fonts is { } fonts && fonts.Renames.TryGetValue(oldNumber, out var rename) && value.AsDictionary() is { } dictionary
            ? FontNaming.Apply(dictionary, rename, Working.Objects.Names)
            : null;
        return renamed is null ? value : PdfValue.FromDictionary(renamed);
    }

    /// <summary>Re-encodes an image as it is written.</summary>
    /// <param name="oldNumber">The image's number in the source.</param>
    /// <param name="image">The image.</param>
    /// <param name="use">How it is used.</param>
    /// <returns>The new image, or <see langword="null"/> to fall back to lossless recompression.</returns>
    private PdfStream? TransformImage(int oldNumber, PdfStream image, ImageUse use)
    {
        var outcome = _recoder.Recode(image, use, _masks.Contains(oldNumber));
        if (outcome.Stream is { } result)
        {
            _report.Changed(PdfOptimizeCategory.Images, oldNumber, outcome.Note, image.RawLength, result.RawLength);
            return result;
        }

        if (outcome.IsSkip)
        {
            _report.Skip(PdfOptimizeCategory.Images, oldNumber, outcome.Note);
        }

        return null;
    }
}
