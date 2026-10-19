// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Text;

/// <summary>
/// A test font for text extraction: one-byte codes, half-em advances (a quarter em for the space) and box outlines.
/// Codes 0xA0 to 0xBA map to Hebrew letters, 0xC0 to 0xDF to Arabic letters, 1 to the three letters "ffi", 3 to the
/// single ligature U+FB01 and 2 to nothing.
/// </summary>
[DebuggerDisplay("TextTestFont: vertical {IsVertical}")]
internal sealed class TextTestFont : PdfFont
{
    /// <summary>The glyph units in one em.</summary>
    internal const float Em = 1000;

    /// <summary>The advance of most glyphs in text space units.</summary>
    internal const float Advance = 0.5F;

    /// <summary>The advance of the space in text space units.</summary>
    internal const float SpaceAdvance = 0.25F;

    /// <summary>The left side bearing of the box outline, in glyph units.</summary>
    internal const float Bearing = 50;

    /// <summary>The top of the box outline, in glyph units.</summary>
    internal const float GlyphTop = 700;

    /// <summary>The code mapped to the three letters "ffi".</summary>
    internal const int LigatureCode = 1;

    /// <summary>The code that has no Unicode text.</summary>
    internal const int UnmappedCode = 2;

    /// <summary>The code mapped to the single ligature U+FB01.</summary>
    internal const int FiLigatureCode = 3;

    /// <summary>The code mapped to the combining acute accent, U+0301.</summary>
    internal const int CombiningAcuteCode = 4;

    /// <summary>The code mapped to the combining dot below, U+0323.</summary>
    internal const int CombiningDotBelowCode = 5;

    /// <summary>The first code mapped to Hebrew.</summary>
    internal const int FirstHebrewCode = 0xA0;

    /// <summary>The last code mapped to Hebrew.</summary>
    internal const int LastHebrewCode = 0xBA;

    /// <summary>The first Hebrew letter, alef.</summary>
    internal const int HebrewAlef = 0x05D0;

    /// <summary>The first code mapped to Arabic.</summary>
    internal const int FirstArabicCode = 0xC0;

    /// <summary>The last code mapped to Arabic.</summary>
    internal const int LastArabicCode = 0xDF;

    /// <summary>The first Arabic letter mapped, hamza.</summary>
    internal const int ArabicHamza = 0x0621;

    /// <summary>The single ligature fi.</summary>
    private const char FiLigature = (char)0xFB01;

    /// <summary>The combining acute accent.</summary>
    private const char CombiningAcute = (char)0x0301;

    /// <summary>The combining dot below.</summary>
    private const char CombiningDotBelow = (char)0x0323;

    /// <summary>The glyph outline.</summary>
    private readonly PdfPath _box;

    /// <summary>Initializes a new instance of the <see cref="TextTestFont"/> class.</summary>
    /// <param name="dictionary">The font dictionary.</param>
    /// <param name="vertical">Whether the font writes top to bottom.</param>
    /// <param name="bold">Whether the font is bold.</param>
    internal TextTestFont(PdfDictionary dictionary, bool vertical, bool bold)
        : base(dictionary)
    {
        IsVertical = vertical;
        IsBold = bold;
        var builder = new PdfPathBuilder();
        builder.AddRect(new(Bearing, 0, (Advance * Em) - Bearing, GlyphTop));
        _box = builder.Detach();
    }

    /// <inheritdoc/>
    public override bool IsVertical { get; }

    /// <inheritdoc/>
    public override bool IsBold { get; }

    /// <inheritdoc/>
    public override int ReadCode(ReadOnlySpan<byte> bytes, out int code)
    {
        code = bytes[0];
        return 1;
    }

    /// <inheritdoc/>
    public override float GetWidth(int code) => code == ' ' ? SpaceAdvance : Advance;

    /// <inheritdoc/>
    public override PdfPath? GetOutline(int code) => code == ' ' ? null : _box;

    /// <inheritdoc/>
    public override int GetUnicode(int code, Span<char> destination)
    {
        switch (code)
        {
            case LigatureCode:
            {
                "ffi".CopyTo(destination);
                return "ffi".Length;
            }

            case UnmappedCode:
            {
                return 0;
            }

            case FiLigatureCode:
            {
                destination[0] = FiLigature;
                return 1;
            }

            case CombiningAcuteCode:
            {
                destination[0] = CombiningAcute;
                return 1;
            }

            case CombiningDotBelowCode:
            {
                destination[0] = CombiningDotBelow;
                return 1;
            }

            default:
            {
                destination[0] = Map(code);
                return 1;
            }
        }
    }

    /// <summary>Maps a code to its character.</summary>
    /// <param name="code">The code.</param>
    /// <returns>The character.</returns>
    private static char Map(int code) => code switch
    {
        >= FirstHebrewCode and <= LastHebrewCode => (char)(HebrewAlef + code - FirstHebrewCode),
        >= FirstArabicCode and <= LastArabicCode => (char)(ArabicHamza + code - FirstArabicCode),
        _ => (char)code,
    };
}
