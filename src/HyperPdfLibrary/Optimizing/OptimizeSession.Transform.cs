// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Optimizing;

/// <content>Re-encoding objects as they are written.</content>
internal sealed partial class OptimizeSession
{
    /// <inheritdoc/>
    public PdfValue Transform(int oldNumber, PdfValue value)
    {
        if (value.AsStream() is not { } stream)
        {
            return value;
        }

        // The writer may call back on another thread, so each object uses this session's token.
        _cancellationToken.ThrowIfCancellationRequested();
        if (_images.TryGetValue(oldNumber, out var use) && TransformImage(oldNumber, stream, use) is { } image)
        {
            return PdfValue.FromStream(image);
        }

        if (_fontGlyphs.TryGetValue(oldNumber, out var glyphs) && TransformFont(oldNumber, stream, glyphs) is { } font)
        {
            return PdfValue.FromStream(font);
        }

        if (_options.RecompressStreams && StreamRecompressor.TryRecompress(stream, _cancellationToken) is { } smaller)
        {
            _report.Measure(PdfOptimizeCategory.Streams, stream.RawLength, smaller.RawLength);
            return PdfValue.FromStream(smaller);
        }

        return value;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Progress(int written, int total, long bytes) => _progress?.Report(new(PdfOptimizePhase.Writing, written, total, bytes));

    /// <summary>Re-encodes an image as it is written.</summary>
    /// <param name="oldNumber">The image's number in the source.</param>
    /// <param name="image">The image.</param>
    /// <param name="use">How it is used.</param>
    /// <returns>The new image, or <see langword="null"/> to fall back to lossless recompression.</returns>
    private PdfStream? TransformImage(int oldNumber, PdfStream image, ImageUse use)
    {
        var outcome = _recoder!.Recode(image, use, _masks.Contains(oldNumber));
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

    /// <summary>Subsets a font program as it is written.</summary>
    /// <param name="oldNumber">The program's number in the source.</param>
    /// <param name="program">The font program stream.</param>
    /// <param name="glyphs">The glyphs to keep.</param>
    /// <returns>The subset program, or <see langword="null"/> when nothing was gained.</returns>
    private PdfStream? TransformFont(int oldNumber, PdfStream program, HashSet<int> glyphs)
    {
        if (TrueTypeSubsetter.Subset(program, glyphs, Names, _cancellationToken) is not { } subset)
        {
            _report.Skip(PdfOptimizeCategory.Fonts, oldNumber, "The font program could not be subset or subsetting saved nothing.");
            return null;
        }

        var note = string.Create(CultureInfo.InvariantCulture, $"TrueType font program cut down to {glyphs.Count} used glyphs; glyph numbers are kept.");
        _report.Changed(PdfOptimizeCategory.Fonts, oldNumber, note, program.RawLength, subset.RawLength);
        return subset;
    }
}
