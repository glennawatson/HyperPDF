// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Fonts.Data;
using HyperPdfLibrary.Objects;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Stands in for a standard 14 font while the library has no font loader, so text benchmarks see real widths and text:
/// one-byte ASCII codes, the standard font's widths and a cap-height box as each outline.
/// </summary>
[DebuggerDisplay("HyperPdfStandInFont")]
internal sealed class HyperPdfStandInFont : PdfFont
{
    /// <summary>The glyph units in one em.</summary>
    private const float Em = 1000;

    /// <summary>The first printable ASCII code.</summary>
    private const int FirstPrintable = 0x20;

    /// <summary>The last printable ASCII code.</summary>
    private const int LastPrintable = 0x7E;

    /// <summary>The font's metrics.</summary>
    private readonly StandardFontMetrics _metrics;

    /// <summary>The box outline of each printable code.</summary>
    private readonly PdfPath?[] _boxes = new PdfPath?[LastPrintable + 1];

    /// <summary>Initializes a new instance of the <see cref="HyperPdfStandInFont"/> class.</summary>
    /// <param name="dictionary">The font dictionary.</param>
    /// <param name="metrics">The standard font's metrics.</param>
    private HyperPdfStandInFont(PdfDictionary dictionary, StandardFontMetrics metrics)
        : base(dictionary)
    {
        _metrics = metrics;
        for (var code = FirstPrintable + 1; code <= LastPrintable; code++)
        {
            var builder = new PdfPathBuilder();
            builder.AddRect(new(0, 0, GetWidth(code) * Em, metrics.CapHeight));
            _boxes[code] = builder.Detach();
        }
    }

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
    public override PdfPath? GetOutline(int code) => code is > FirstPrintable and <= LastPrintable ? _boxes[code] : null;

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
    internal static HyperPdfStandInFont? Create(PdfDictionary dictionary)
    {
        var names = dictionary.Owner?.Names;
        return names is not null && StandardFonts.TryGet(names.GetSpelling(dictionary.GetName(KnownName.BaseFont)), out var metrics)
            ? new(dictionary, metrics)
            : null;
    }
}
