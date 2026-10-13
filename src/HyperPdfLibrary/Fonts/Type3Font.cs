// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using HyperPdfLibrary.Fonts.CMaps;
using HyperPdfLibrary.Fonts.Data;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Fonts;

/// <summary>
/// A /Type3 font, (ISO 32000-2, 9.6.4): the font matrix, /Widths scaled by the matrix's x scale
/// and rounded to glyph units, and each code's /CharProcs stream found through the encoding's glyph names.
/// </summary>
[DebuggerDisplay("Type3Font")]
internal sealed class Type3Font : PdfType3Font
{
    /// <summary>The glyph space units per text space unit.</summary>
    private const float GlyphUnits = 1000;

    /// <summary>The default font matrix entry.</summary>
    private const float DefaultScale = 0.001F;

    /// <summary>The number of entries in a font matrix.</summary>
    private const int MatrixEntries = 6;

    /// <summary>The index of the matrix's x offset.</summary>
    private const int MatrixE = 4;

    /// <summary>The index of the matrix's y offset.</summary>
    private const int MatrixF = 5;

    /// <summary>The index of the matrix's y shear.</summary>
    private const int MatrixB = 1;

    /// <summary>The index of the matrix's x shear.</summary>
    private const int MatrixC = 2;

    /// <summary>The index of the matrix's y scale.</summary>
    private const int MatrixD = 3;

    /// <summary>The widths in text space units, already scaled by the font matrix.</summary>
    private readonly float[] _widths = new float[FontEncodings.CodeCount];

    /// <summary>The glyph procedure of each code.</summary>
    private readonly PdfStream?[] _procedures = new PdfStream?[FontEncodings.CodeCount];

    /// <summary>The text of each code from its glyph name.</summary>
    private readonly string?[] _texts = new string?[FontEncodings.CodeCount];

    /// <summary>The /ToUnicode map, or <see langword="null"/>.</summary>
    private readonly ToUnicodeMap? _toUnicode;

    /// <summary>Initializes a new instance of the <see cref="Type3Font"/> class.</summary>
    /// <param name="dictionary">The font dictionary.</param>
    /// <param name="resources">The resources selected by the font loader.</param>
    internal Type3Font(PdfDictionary dictionary, PdfFontResources resources)
        : base(dictionary)
    {
        FontMatrix = ReadMatrix(dictionary.GetArray(KnownName.FontMatrix));
        Resources = dictionary.GetDictionary(KnownName.Resources);
        _toUnicode = ToUnicodeLoader.Load(dictionary);
        ReadWidths(dictionary, FontMatrix.M11);
        var encoding = dictionary.Get(KnownName.Encoding).IsNull
            ? new SimpleEncoding(FontEncoding.None, null)
            : SimpleEncoding.Read(dictionary, new(FontEncoding.None, false, false, false, false));
        ReadProcedures(dictionary, encoding, resources);
    }

    /// <inheritdoc/>
    public override Matrix3x2 FontMatrix { get; }

    /// <inheritdoc/>
    public override PdfDictionary? Resources { get; }

    /// <inheritdoc/>
    public override int ReadCode(ReadOnlySpan<byte> bytes, out int code)
    {
        if (bytes.IsEmpty)
        {
            code = 0;
            return 0;
        }

        code = bytes[0];
        return 1;
    }

    /// <inheritdoc/>
    public override float GetWidth(int code) => _widths[(uint)code < FontEncodings.CodeCount ? code : 0];

    /// <inheritdoc/>
    public override PdfStream? GetCharProc(int code) => (uint)code < FontEncodings.CodeCount ? _procedures[code] : null;

    /// <inheritdoc/>
    public override int GetUnicode(int code, Span<char> destination)
    {
        if (_toUnicode is { } map && map.TryGetUnicode(code, destination, out var written) && written > 0)
        {
            return written;
        }

        var text = (uint)code < FontEncodings.CodeCount ? _texts[code] : null;
        return text is not null && text.AsSpan().TryCopyTo(destination) ? text.Length : 0;
    }

    /// <summary>Reads /FontMatrix, defaulting to a 1/1000 scale.</summary>
    /// <param name="array">The array, or <see langword="null"/>.</param>
    /// <returns>The matrix.</returns>
    private static Matrix3x2 ReadMatrix(PdfArray? array) => array is null || array.Count < MatrixEntries
        ? new(DefaultScale, 0, 0, DefaultScale, 0, 0)
        : new(array.GetSingle(0), array.GetSingle(MatrixB), array.GetSingle(MatrixC), array.GetSingle(MatrixD), array.GetSingle(MatrixE), array.GetSingle(MatrixF));

    /// <summary>Finds a /CharProcs entry by its key's spelling.</summary>
    /// <param name="procedures">The /CharProcs dictionary.</param>
    /// <param name="name">The glyph name.</param>
    /// <returns>The stream, or <see langword="null"/>.</returns>
    private static PdfStream? FindProcedure(PdfDictionary procedures, ReadOnlySpan<byte> name)
    {
        for (var i = 0; i < procedures.Count; i++)
        {
            if (PdfNames.Spell(procedures, procedures.GetKeyAt(i)).SequenceEqual(name))
            {
                return procedures.Get(procedures.GetKeyAt(i)).AsStream();
            }
        }

        return null;
    }

    /// <summary>Reads /Widths from /FirstChar, scaled by the matrix's x scale and rounded to glyph units.</summary>
    /// <param name="dictionary">The font dictionary.</param>
    /// <param name="scale">The matrix's x scale.</param>
    private void ReadWidths(PdfDictionary dictionary, float scale)
    {
        var first = dictionary.GetInt32(KnownName.FirstChar);
        if ((uint)first >= FontEncodings.CodeCount || dictionary.GetArray(KnownName.Widths) is not { } widths)
        {
            return;
        }

        var count = Math.Min(widths.Count, FontEncodings.CodeCount - first);
        for (var i = 0; i < count; i++)
        {
            _widths[first + i] = MathF.Round(widths.GetSingle(i) * scale * GlyphUnits) / GlyphUnits;
        }
    }

    /// <summary>Finds each code's glyph procedure by its glyph name.</summary>
    /// <param name="dictionary">The font dictionary.</param>
    /// <param name="encoding">The encoding.</param>
    /// <param name="resources">The selected font resources.</param>
    private void ReadProcedures(PdfDictionary dictionary, SimpleEncoding encoding, PdfFontResources resources)
    {
        if (dictionary.GetDictionary(KnownName.CharProcs) is not { } procedures)
        {
            return;
        }

        for (var code = 0; code < FontEncodings.CodeCount; code++)
        {
            var name = encoding.GetName(code);
            if (name.IsEmpty)
            {
                continue;
            }

            _texts[code] = GlyphNameText.TextOf(resources, name, false);
            _procedures[code] = FindProcedure(procedures, name);
        }
    }
}
