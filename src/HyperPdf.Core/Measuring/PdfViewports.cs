// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Globalization;
using HyperPdf.Core.Signatures.Signing;

namespace HyperPdf.Core.Measuring;

/// <summary>
/// Reads the scale a drawing declares for a page: the <c>/Measure</c> dictionary of the page's first viewport
/// (<c>/VP</c>), which engineering and architectural PDFs use to say, for example, that 1 in on paper is 10 ft.
/// </summary>
public static class PdfViewports
{
    /// <summary>The characters of text read on the stack.</summary>
    private const int StackChars = 256;

    /// <summary>The characters of the ratio text built for a number format's conversion.</summary>
    private const int ConversionChars = 128;

    /// <summary>Reads a page's declared scale.</summary>
    /// <param name="file">The PDF's bytes.</param>
    /// <param name="pageIndex">The page, zero-based.</param>
    /// <returns>The scale, or <see langword="null"/> when the page declares none or the file cannot be read.</returns>
    public static MeasureScale? ReadScale(byte[] file, int pageIndex)
    {
        ArgumentNullException.ThrowIfNull(file);
        try
        {
            var structure = PdfReader.Read(file);
            var catalog = PdfReader.GetObject(structure, PdfPages.ReadReference(structure.Trailer, "Root"u8));
            var page = PdfReader.GetObject(structure, PdfPages.FindPage(structure, catalog, pageIndex));
            var viewports = PdfSyntax.FindKey(page.Span, 0, "VP"u8);
            if (viewports < 0)
            {
                return null;
            }

            var array = PdfReader.Resolve(structure, page, viewports);
            var first = PdfSyntax.SkipSpace(array.Span, 1);
            var viewport = PdfReader.Resolve(structure, array, first);
            var measureAt = PdfSyntax.FindKey(viewport.Span, 0, "Measure"u8);
            return measureAt < 0 ? null : FromMeasure(structure, PdfReader.Resolve(structure, viewport, measureAt));
        }
        catch (InvalidDataException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>Reads a rectilinear measure dictionary: its ratio text, or its first number format's conversion.</summary>
    /// <param name="structure">The file structure.</param>
    /// <param name="measure">The measure dictionary.</param>
    /// <returns>The scale, or <see langword="null"/>.</returns>
    private static MeasureScale? FromMeasure(PdfStructure structure, ReadOnlyMemory<byte> measure)
    {
        var ratioAt = PdfSyntax.FindKey(measure.Span, 0, "R"u8);
        if (ratioAt >= 0 && TryParseText(PdfReader.Resolve(structure, measure, ratioAt).Span, out var stated))
        {
            return stated;
        }

        // Otherwise the first /X number format says how many of its unit one point is: /C factor, /U unit.
        var formatsAt = PdfSyntax.FindKey(measure.Span, 0, "X"u8);
        if (formatsAt < 0)
        {
            return null;
        }

        var formats = PdfReader.Resolve(structure, measure, formatsAt);
        var format = PdfReader.Resolve(structure, formats, PdfSyntax.SkipSpace(formats.Span, 1));
        var factorAt = PdfSyntax.FindKey(format.Span, 0, "C"u8);
        var unitAt = PdfSyntax.FindKey(format.Span, 0, "U"u8);
        if (factorAt < 0 || unitAt < 0)
        {
            return null;
        }

        return TryConvert(format.Span[factorAt..PdfSyntax.ValueEnd(format.Span, factorAt)], PdfReader.Resolve(structure, format, unitAt).Span, out var converted) ? converted : null;
    }

    /// <summary>Reads a PDF string holding a scale written as text, such as "1 cm = 2 m".</summary>
    /// <param name="value">The string value.</param>
    /// <param name="scale">The scale.</param>
    /// <returns><see langword="true"/> when the string is a scale.</returns>
    private static bool TryParseText(ReadOnlySpan<byte> value, out MeasureScale scale)
    {
        char[]? rented = null;
        var text = value.Length <= StackChars ? stackalloc char[StackChars] : (rented = ArrayPool<char>.Shared.Rent(value.Length));
        try
        {
            return MeasureScale.TryParse(text[..PdfText.Decode(value, text)], out scale);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }
    }

    /// <summary>Reads a number format's factor and unit as the scale "1 pt = factor unit".</summary>
    /// <param name="factorBytes">The factor as written in the file.</param>
    /// <param name="unitValue">The unit's string value.</param>
    /// <param name="scale">The scale.</param>
    /// <returns><see langword="true"/> when the factor is a positive number and the unit is known.</returns>
    private static bool TryConvert(ReadOnlySpan<byte> factorBytes, ReadOnlySpan<byte> unitValue, out MeasureScale scale)
    {
        scale = default;
        if (!double.TryParse(factorBytes, NumberStyles.Float, CultureInfo.InvariantCulture, out var factor) || factor <= 0 || unitValue.Length > StackChars)
        {
            return false;
        }

        Span<char> unit = stackalloc char[StackChars];
        var unitText = unit[..PdfText.Decode(unitValue, unit)].Trim();
        Span<char> text = stackalloc char[ConversionChars];
        "1 pt = ".CopyTo(text);
        var length = "1 pt = ".Length;
        if (!factor.TryFormat(text[length..], out var written, default, CultureInfo.InvariantCulture) || length + written + 1 + unitText.Length > text.Length)
        {
            return false;
        }

        length += written;
        text[length] = ' ';
        length++;
        unitText.CopyTo(text[length..]);
        return MeasureScale.TryParse(text[..(length + unitText.Length)], out scale);
    }
}
