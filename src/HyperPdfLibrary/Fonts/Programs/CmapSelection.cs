// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Fonts.Programs;

/// <summary>The cmap subtables a TrueType font program uses, as absolute offsets into the font data, or -1 when missing.</summary>
/// <param name="UnicodeFull">The full-repertoire Unicode subtable: (3,10) or (0,4) and above.</param>
/// <param name="UnicodeBmp">The BMP Unicode subtable: (3,1) or (0,0) to (0,3).</param>
/// <param name="Symbol">The Microsoft symbol subtable (3,0).</param>
/// <param name="Mac">The Macintosh Roman subtable (1,0).</param>
[DebuggerDisplay("CmapSelection: {UnicodeBmp} {Symbol} {Mac}")]
internal readonly record struct CmapSelection(int UnicodeFull, int UnicodeBmp, int Symbol, int Mac)
{
    /// <summary>The Unicode platform.</summary>
    private const int UnicodePlatform = 0;

    /// <summary>The Macintosh platform.</summary>
    private const int MacPlatform = 1;

    /// <summary>The Windows platform.</summary>
    private const int WindowsPlatform = 3;

    /// <summary>The Windows symbol encoding.</summary>
    private const int WindowsSymbol = 0;

    /// <summary>The Windows Unicode BMP encoding.</summary>
    private const int WindowsBmp = 1;

    /// <summary>The Windows full Unicode encoding.</summary>
    private const int WindowsFull = 10;

    /// <summary>The first Unicode-platform encoding that covers the full repertoire.</summary>
    private const int UnicodeFullEncoding = 4;

    /// <summary>The offset of the subtable count.</summary>
    private const int CountOffset = 2;

    /// <summary>The offset of the first encoding record.</summary>
    private const int RecordsOffset = 4;

    /// <summary>The size of one encoding record.</summary>
    private const int RecordSize = 8;

    /// <summary>The offset of a record's subtable offset.</summary>
    private const int RecordSubtable = 4;

    /// <summary>Gets a selection with no subtables.</summary>
    internal static CmapSelection None => new(-1, -1, -1, -1);

    /// <summary>Chooses the subtables of a 'cmap' table. The first supported subtable of each kind wins.</summary>
    /// <param name="data">The font data.</param>
    /// <param name="cmap">The 'cmap' table.</param>
    /// <returns>The selection.</returns>
    /// <exception cref="PdfException">The parsed range exceeds the available data or its numeric width.</exception>
    internal static CmapSelection Read(ReadOnlySpan<byte> data, TableRange cmap)
    {
        var selection = None;
        var table = cmap.Of(data);
        var count = Math.Min(FontBytes.U16(table, CountOffset), Math.Max((table.Length - RecordsOffset) / RecordSize, 0));
        for (var i = 0; i < count; i++)
        {
            var record = RecordsOffset + (i * RecordSize);
            var subtable = (long)cmap.Offset + FontBytes.U32(table, record + RecordSubtable);
            if (subtable > (long)cmap.Offset + cmap.Length - FontBytes.U16Size)
            {
                throw new PdfException(PdfError.Format, "The TrueType cmap subtable offset exceeds its table.");
            }

            if (TrueTypeCmap.IsSupported(data, (int)subtable))
            {
                selection = selection.With(FontBytes.U16(table, record), FontBytes.U16(table, record + FontBytes.U16Size), (int)subtable);
            }
        }

        return selection;
    }

    /// <summary>Records a subtable in its slot when the slot is still empty.</summary>
    /// <param name="platform">The platform id.</param>
    /// <param name="encoding">The encoding id.</param>
    /// <param name="offset">The subtable offset.</param>
    /// <returns>The updated selection.</returns>
    private CmapSelection With(int platform, int encoding, int offset) => new CmapEncodingId(platform, encoding) switch
    {
        (WindowsPlatform, WindowsFull) when UnicodeFull < 0 => this with { UnicodeFull = offset },
        (WindowsPlatform, WindowsBmp) when UnicodeBmp < 0 => this with { UnicodeBmp = offset },
        (WindowsPlatform, WindowsSymbol) when Symbol < 0 => this with { Symbol = offset },
        (MacPlatform, 0) when Mac < 0 => this with { Mac = offset },
        (UnicodePlatform, >= UnicodeFullEncoding) when UnicodeFull < 0 => this with { UnicodeFull = offset },
        (UnicodePlatform, _) when UnicodeBmp < 0 => this with { UnicodeBmp = offset },
        _ => this,
    };
}
