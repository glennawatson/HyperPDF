// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Numerics;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.TextLayer;

/// <summary>
/// Writes recognised words onto a page as invisible text (render mode 3), each word scaled to the box it was found in,
/// so search, selection and copy work on scanned pages without changing how they look. Each word fills its box:
/// the font size makes the font's ascent and descent as tall as the box, and horizontal
/// scaling makes the glyph run as wide. The text follows the page's rotation and crop box.
/// </summary>
public static class PdfTextLayer
{
    /// <summary>The invisible text render mode.</summary>
    private const int Invisible = 3;

    /// <summary>Boxes smaller than this, in points, are noise.</summary>
    private const float MinimumSize = 0.5F;

    /// <summary>Thousandths of an em.</summary>
    private const float Thousand = 1000;

    /// <summary>The percent that means no horizontal scaling.</summary>
    private const float Percent = 100;

    /// <summary>The most bytes a character needs: a two-byte code.</summary>
    private const int BytesPerChar = 2;

    /// <summary>The most fonts whose names and use flags are kept on the stack; more are kept in arrays.</summary>
    private const int StackFonts = 32;

    /// <summary>Determines whether a word has text and a box big enough to place.</summary>
    /// <param name="word">The word.</param>
    /// <returns><see langword="true"/> when the writer would place it.</returns>
    public static bool IsPlaceable(in PdfTextLayerWord word)
    {
        var width = word.Right - word.Left;
        var height = word.Bottom - word.Top;
        return !string.IsNullOrEmpty(word.Text) && float.IsFinite(width) && float.IsFinite(height) && width >= MinimumSize && height >= MinimumSize;
    }

    /// <summary>
    /// Adds the words to a page as a new content stream and wraps the existing content so its graphics state cannot
    /// affect the words. Callers serialise edits to one document.
    /// </summary>
    /// <param name="store">The document.</param>
    /// <param name="page">The page; it must be an object of its own.</param>
    /// <param name="words">The words, in viewer space.</param>
    /// <param name="fonts">The fonts words refer to by index.</param>
    /// <returns>The number of words written.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static int Append(PdfObjectStore store, PdfPage page, ReadOnlySpan<PdfTextLayerWord> words, ReadOnlySpan<IPdfTextLayerFont> fonts)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(page);
        if (words.IsEmpty || fonts.IsEmpty || !page.Id.IsValid)
        {
            return 0;
        }

        Span<int> numbers = fonts.Length <= StackFonts ? stackalloc int[StackFonts] : new int[fonts.Length];
        Span<bool> used = fonts.Length <= StackFonts ? stackalloc bool[StackFonts] : new bool[fonts.Length];
        numbers = numbers[..fonts.Length];
        used = used[..fonts.Length];
        used.Clear();
        PdfTextLayerPage.ChooseNames(store, page, numbers);
        var codes = ArrayPool<byte>.Shared.Rent(MaxCodeBytes(words));
        var builder = default(PdfContentBuilder);
        try
        {
            var written = WriteWords(ref builder, page.UserTransform, words, fonts, numbers, used, codes);
            return written > 0 && PdfTextLayerPage.Install(store, page, fonts, numbers, used, builder.WrittenSpan) ? written : 0;
        }
        finally
        {
            builder.Dispose();
            ArrayPool<byte>.Shared.Return(codes);
        }
    }

    /// <summary>Gets the room needed for the codes of the longest word.</summary>
    /// <param name="words">The words.</param>
    /// <returns>The number of bytes.</returns>
    private static int MaxCodeBytes(ReadOnlySpan<PdfTextLayerWord> words)
    {
        var longest = 0;
        foreach (ref readonly var word in words)
        {
            longest = Math.Max(longest, word.Text?.Length ?? 0);
        }

        return (longest * BytesPerChar) + BytesPerChar;
    }

    /// <summary>Writes the text object holding every placeable word.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="userTransform">Maps viewer space to user space.</param>
    /// <param name="words">The words.</param>
    /// <param name="fonts">The fonts.</param>
    /// <param name="numbers">The number in the resource name of each font.</param>
    /// <param name="used">Receives which fonts were used.</param>
    /// <param name="codes">Scratch space for a word's codes.</param>
    /// <returns>The number of words written.</returns>
    private static int WriteWords(
        ref PdfContentBuilder builder,
        Matrix3x2 userTransform,
        ReadOnlySpan<PdfTextLayerWord> words,
        ReadOnlySpan<IPdfTextLayerFont> fonts,
        scoped ReadOnlySpan<int> numbers,
        scoped Span<bool> used,
        byte[] codes)
    {
        builder.SaveState();
        builder.BeginText();
        builder.SetTextRenderingMode(Invisible);
        Span<byte> nameBuffer = stackalloc byte[PdfTextLayerPage.MaxNameLength];
        var written = 0;
        foreach (ref readonly var word in words)
        {
            if ((uint)word.Font >= (uint)fonts.Length || !IsPlaceable(word))
            {
                continue;
            }

            var name = PdfTextLayerPage.FormatName(numbers[word.Font], nameBuffer);
            if (!WriteWord(ref builder, userTransform, word, fonts[word.Font], name, codes))
            {
                continue;
            }

            used[word.Font] = true;
            written++;
        }

        builder.EndText();
        builder.RestoreState();
        return written;
    }

    /// <summary>Writes one word so its glyph run fills its box.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="userTransform">Maps viewer space to user space.</param>
    /// <param name="word">The word.</param>
    /// <param name="font">Its font.</param>
    /// <param name="name">The font's resource name.</param>
    /// <param name="codes">Scratch space for the codes.</param>
    /// <returns><see langword="true"/> when the word was written.</returns>
    private static bool WriteWord(ref PdfContentBuilder builder, Matrix3x2 userTransform, in PdfTextLayerWord word, IPdfTextLayerFont font, scoped ReadOnlySpan<byte> name, byte[] codes)
    {
        var span = font.Ascent + font.Descent;
        var count = font.Encode(word.Text, codes, out var extent);
        var ink = extent.InkRight - extent.InkLeft;
        if (!(span > 0) || count == 0 || !(ink > 0))
        {
            return false;
        }

        // The ink fills the box: scale the run to the box and shift its ink to the left edge.
        var size = (word.Bottom - word.Top) / span;
        var scale = (word.Right - word.Left) / (ink * size / Thousand);
        var left = word.Left - (scale * extent.InkLeft * size / Thousand);
        var origin = Vector2.Transform(new(left, word.Bottom - (font.Descent * size)), userTransform);
        builder.SetFont(name, size);
        builder.SetHorizontalScaling(scale * Percent);

        // The text's x axis runs along viewer x and its y axis up the viewer, whatever the page rotation.
        builder.SetTextMatrix(userTransform.M11, userTransform.M12, -userTransform.M21, -userTransform.M22, origin.X, origin.Y);
        builder.ShowText(codes.AsSpan(0, count));
        return true;
    }
}
