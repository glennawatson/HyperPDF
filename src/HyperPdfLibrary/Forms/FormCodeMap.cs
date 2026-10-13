// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Text;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <summary>
/// Finds the code a font shows a character with. It reads the font's own meaning of each code (its encoding and
/// <c>/Differences</c>, or its CMap and <c>/ToUnicode</c>) and turns that around, so text is written in the font's
/// encoding and not always in WinAnsi. Legacy multi-byte CMaps such as 90ms-RKSJ-H or GBK-EUC-H, which mix one-byte and
/// two-byte codes, are read through the CMap's own code lengths (ISO 32000-2 9.7.5).
/// </summary>
[DebuggerDisplay("FormCodeMap: {UnitBytes} byte codes")]
internal sealed class FormCodeMap
{
    /// <summary>The number of codes of a one-byte font.</summary>
    private const int ByteCodes = 256;

    /// <summary>The number of codes of a two-byte font.</summary>
    private const int WideCodes = 65_536;

    /// <summary>The bytes in a code of a Type0 font with a fixed code length.</summary>
    private const int WideBytes = 2;

    /// <summary>The most characters one code maps to that are looked at.</summary>
    private const int MaxText = 8;

    /// <summary>The characters the font shows, ascending.</summary>
    private readonly int[] _characters;

    /// <summary>The code to show for each character, parallel to <see cref="_characters"/>.</summary>
    private readonly FormCode[] _codes;

    /// <summary>Whether a character's code is the character itself (the Unicode CMaps).</summary>
    private readonly bool _isUnicode;

    /// <summary>Initializes a new instance of the <see cref="FormCodeMap"/> class.</summary>
    /// <param name="unitBytes">The bytes in a code; 0 when the codes differ in length.</param>
    /// <param name="characters">The characters the font shows, ascending; empty for a Unicode CMap.</param>
    /// <param name="codes">The code for each character; empty for a Unicode CMap.</param>
    /// <param name="isUnicode">Whether a character's code is the character itself.</param>
    private FormCodeMap(int unitBytes, int[] characters, FormCode[] codes, bool isUnicode)
    {
        UnitBytes = unitBytes;
        _characters = characters;
        _codes = codes;
        _isUnicode = isUnicode;
    }

    /// <summary>Gets the bytes in a code; 0 when the font's codes are one or two bytes long.</summary>
    internal int UnitBytes { get; }

    /// <summary>Makes the map of a font.</summary>
    /// <param name="font">The loaded font.</param>
    /// <param name="names">The document's names.</param>
    /// <returns>The map.</returns>
    internal static FormCodeMap Create(PdfFont font, PdfNameTable names)
    {
        if (!font.Dictionary.IsName(KnownName.Subtype, KnownName.Type0))
        {
            return Scan(font, 1, ByteCodes);
        }

        var encoding = font.Dictionary.Get(KnownName.Encoding);
        if (encoding.Kind != PdfKind.Name)
        {
            return ScanMixed(font);
        }

        var name = names.GetString(encoding.AsName());
        if (name.Contains("UCS2", StringComparison.Ordinal) && name.EndsWith("-H", StringComparison.Ordinal))
        {
            return new(WideBytes, [], [], true);
        }

        return name.EndsWith("Identity-H", StringComparison.Ordinal) ? Scan(font, WideBytes, WideCodes) : ScanMixed(font);
    }

    /// <summary>Finds the code a character is shown with.</summary>
    /// <param name="rune">The character.</param>
    /// <param name="code">The code.</param>
    /// <returns><see langword="true"/> when the font has the character.</returns>
    internal bool TryGetCode(Rune rune, out FormCode code)
    {
        if (_isUnicode && rune.Value < WideCodes)
        {
            code = new(rune.Value, WideBytes);
            return true;
        }

        var index = Array.BinarySearch(_characters, rune.Value);
        code = index >= 0 ? _codes[index] : default;
        return index >= 0;
    }

    /// <summary>Reads what each code of a font stands for and turns it around.</summary>
    /// <param name="font">The font.</param>
    /// <param name="length">The bytes in each code.</param>
    /// <param name="count">The number of codes to read.</param>
    /// <returns>The map, holding the lowest code for each character.</returns>
    private static FormCodeMap Scan(PdfFont font, int length, int count)
    {
        var codes = default(FormCodeCollector);
        Span<char> text = stackalloc char[MaxText];
        for (var code = 0; code < count; code++)
        {
            AddCode(font, code, length, text, ref codes);
        }

        var values = codes.Finish(out var characters);
        return new(length, characters, values, false);
    }

    /// <summary>
    /// Reads the codes of a font whose CMap may mix one-byte and two-byte codes. The CMap's codespace says how long the
    /// code that starts with each byte is, so every one-byte code and every two-byte code is tried.
    /// </summary>
    /// <param name="font">The font.</param>
    /// <returns>The map, whose codes keep the length the CMap gives them.</returns>
    private static FormCodeMap ScanMixed(PdfFont font)
    {
        var codes = default(FormCodeCollector);
        Span<char> text = stackalloc char[MaxText];
        Span<byte> bytes = stackalloc byte[WideBytes];
        for (var lead = 0; lead < ByteCodes; lead++)
        {
            bytes[0] = (byte)lead;
            bytes[1] = 0;
            if (font.ReadCode(bytes, out _) == 1)
            {
                AddCode(font, lead, 1, text, ref codes);
                continue;
            }

            for (var trail = 0; trail < ByteCodes; trail++)
            {
                bytes[1] = (byte)trail;
                if (font.ReadCode(bytes, out var code) == WideBytes)
                {
                    AddCode(font, code, WideBytes, text, ref codes);
                }
            }
        }

        var values = codes.Finish(out var characters);
        return new(0, characters, values, false);
    }

    /// <summary>Records the character a code stands for, keeping the lowest code for a character.</summary>
    /// <param name="font">The font.</param>
    /// <param name="code">The code.</param>
    /// <param name="length">The bytes in the code.</param>
    /// <param name="text">Space for the code's text.</param>
    /// <param name="codes">Receives the code.</param>
    private static void AddCode(PdfFont font, int code, int length, scoped Span<char> text, ref FormCodeCollector codes)
    {
        var written = font.GetUnicode(code, text);
        if (written > 0 && Rune.DecodeFromUtf16(text[..written], out var rune, out var consumed) == OperationStatus.Done && consumed == written)
        {
            codes.Add(rune.Value, new(code, length));
        }
    }
}
