// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Text;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <summary>
/// Finds the code a font shows a character with. It reads the font's own meaning of each code (its encoding and
/// <c>/Differences</c>, or its CMap and <c>/ToUnicode</c>) and turns that around, so text is written in the font's
/// encoding and not always in WinAnsi.
/// </summary>
[DebuggerDisplay("FormCodeMap: {UnitBytes} byte codes")]
internal sealed class FormCodeMap
{
    /// <summary>The number of codes of a one-byte font.</summary>
    private const int ByteCodes = 256;

    /// <summary>The number of codes of a two-byte font.</summary>
    private const int WideCodes = 65_536;

    /// <summary>The bytes in a code of a Type0 font.</summary>
    private const int WideBytes = 2;

    /// <summary>The most characters one code maps to that are looked at.</summary>
    private const int MaxText = 8;

    /// <summary>The code to show for each character.</summary>
    private readonly Dictionary<int, int> _codes;

    /// <summary>Whether a character's code is the character itself (the Unicode CMaps).</summary>
    private readonly bool _isUnicode;

    /// <summary>Initializes a new instance of the <see cref="FormCodeMap"/> class.</summary>
    /// <param name="unitBytes">The bytes in a code.</param>
    /// <param name="codes">The code for each character; empty for a Unicode CMap.</param>
    /// <param name="isUnicode">Whether a character's code is the character itself.</param>
    private FormCodeMap(int unitBytes, Dictionary<int, int> codes, bool isUnicode)
    {
        UnitBytes = unitBytes;
        _codes = codes;
        _isUnicode = isUnicode;
    }

    /// <summary>Gets the bytes in a code.</summary>
    internal int UnitBytes { get; }

    /// <summary>Makes the map of a font.</summary>
    /// <param name="font">The loaded font.</param>
    /// <param name="names">The document's names.</param>
    /// <returns>The map, or <see langword="null"/> for a font written with a legacy multi-byte CMap.</returns>
    internal static FormCodeMap? Create(PdfFont font, PdfNameTable names)
    {
        if (!font.Dictionary.IsName(KnownName.Subtype, KnownName.Type0))
        {
            return new(1, Scan(font, ByteCodes), false);
        }

        var encoding = font.Dictionary.Get(KnownName.Encoding);
        if (encoding.Kind != PdfKind.Name)
        {
            return new(WideBytes, Scan(font, WideCodes), false);
        }

        var name = names.GetString(encoding.AsName());
        if (name.Contains("UCS2", StringComparison.Ordinal) && name.EndsWith("-H", StringComparison.Ordinal))
        {
            return new(WideBytes, [], true);
        }

        return name.EndsWith("Identity-H", StringComparison.Ordinal) ? new(WideBytes, Scan(font, WideCodes), false) : null;
    }

    /// <summary>Finds the code a character is shown with.</summary>
    /// <param name="rune">The character.</param>
    /// <param name="code">The code.</param>
    /// <returns><see langword="true"/> when the font has the character.</returns>
    internal bool TryGetCode(Rune rune, out int code)
    {
        if (_isUnicode && rune.Value < WideCodes)
        {
            code = rune.Value;
            return true;
        }

        return _codes.TryGetValue(rune.Value, out code);
    }

    /// <summary>Reads what each code of a font stands for and turns it around.</summary>
    /// <param name="font">The font.</param>
    /// <param name="count">The number of codes to read.</param>
    /// <returns>The lowest code for each character.</returns>
    private static Dictionary<int, int> Scan(PdfFont font, int count)
    {
        var codes = new Dictionary<int, int>();
        Span<char> text = stackalloc char[MaxText];
        for (var code = 0; code < count; code++)
        {
            var written = font.GetUnicode(code, text);
            if (written > 0 && Rune.DecodeFromUtf16(text[..written], out var rune, out var consumed) == System.Buffers.OperationStatus.Done && consumed == written)
            {
                _ = codes.TryAdd(rune.Value, code);
            }
        }

        return codes;
    }
}
