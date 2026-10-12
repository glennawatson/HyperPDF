// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Fonts.Data;
using HyperPdfLibrary.Objects;

namespace HyperPdf.Engine.Tests;

/// <summary>
/// Stands in for a standard 14 font until the library loads real fonts: one-byte codes, ASCII text and the standard
/// font's advance widths, ascent and descent, with a box from the baseline to the cap height as each outline. Widths
/// and text match PDFium's, so characters, generated spaces, line breaks and search line up; outline boxes do not.
/// </summary>
[DebuggerDisplay("StandInFont: {_metrics.Name}")]
internal sealed class StandInFont : PdfFont
{
    /// <summary>The glyph units in one em.</summary>
    private const float Em = 1000;

    /// <summary>The first printable ASCII code.</summary>
    private const int FirstPrintable = 0x20;

    /// <summary>The last printable ASCII code.</summary>
    private const int LastPrintable = 0x7E;

    /// <summary>The font's metrics.</summary>
    private readonly StandardFontMetrics _metrics;

    /// <summary>The box outline of each code, made on first use.</summary>
    private readonly PdfPath?[] _boxes = new PdfPath?[LastPrintable + 1];

    /// <summary>Guards making outlines.</summary>
    private readonly Lock _gate = new();

    /// <summary>Initializes a new instance of the <see cref="StandInFont"/> class.</summary>
    /// <param name="dictionary">The font dictionary.</param>
    /// <param name="metrics">The standard font's metrics.</param>
    private StandInFont(PdfDictionary dictionary, StandardFontMetrics metrics)
        : base(dictionary) => _metrics = metrics;

    /// <inheritdoc/>
    public override bool IsBold => _metrics.IsBold;

    /// <inheritdoc/>
    public override float Ascent => _metrics.Ascent / Em;

    /// <inheritdoc/>
    public override float Descent => _metrics.Descent / Em;

    /// <inheritdoc/>
    public override int ReadCode(ReadOnlySpan<byte> bytes, out int code)
    {
        code = bytes[0];
        return 1;
    }

    /// <inheritdoc/>
    public override float GetWidth(int code) => _metrics.TryGetWidth(code, out var width) ? width / Em : 0;

    /// <inheritdoc/>
    public override PdfPath? GetOutline(int code)
    {
        if (code is <= FirstPrintable or > LastPrintable)
        {
            return null;
        }

        lock (_gate)
        {
            if (_boxes[code] is { } existing)
            {
                return existing;
            }

            var builder = new PdfPathBuilder();
            builder.AddRect(new(0, 0, GetWidth(code) * Em, _metrics.CapHeight));
            var box = builder.Detach();
            _boxes[code] = box;
            return box;
        }
    }

    /// <inheritdoc/>
    public override int GetUnicode(int code, Span<char> destination)
    {
        if (code is < FirstPrintable or > LastPrintable)
        {
            return 0;
        }

        destination[0] = (char)code;
        return 1;
    }

    /// <summary>Makes a stand-in for a standard font dictionary.</summary>
    /// <param name="dictionary">The font dictionary.</param>
    /// <returns>The font, or <see langword="null"/> when the dictionary is not a standard font.</returns>
    internal static StandInFont? Create(PdfDictionary dictionary)
    {
        var names = dictionary.Owner?.Names;
        return names is not null && StandardFonts.TryGet(names.GetSpelling(dictionary.GetName(KnownName.BaseFont)), out var metrics)
            ? new(dictionary, metrics)
            : null;
    }
}
