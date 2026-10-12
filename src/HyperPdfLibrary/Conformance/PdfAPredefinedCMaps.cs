// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Frozen;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// The predefined CMaps PDF/A allows a composite font to name, with the character collection each uses in PDF 1.4, PDF
/// 1.7 and PDF 2.0 (ISO 32000-1, 9.7.5.2, Table 118; ISO 32000-2, Table 116). PDF 1.4 does not define the UTF-16 CMaps.
/// </summary>
internal static class PdfAPredefinedCMaps
{
    /// <summary>The registry of every predefined character collection.</summary>
    internal const string Registry = "Adobe";

    /// <summary>The fields of a table row: name, ordering and the three supplements.</summary>
    private const int RowFields = 5;

    /// <summary>The field of the PDF 1.4 supplement.</summary>
    private const int Pdf14Field = 2;

    /// <summary>The field of the PDF 1.7 supplement.</summary>
    private const int Pdf17Field = 3;

    /// <summary>The field of the PDF 2.0 supplement.</summary>
    private const int Pdf20Field = 4;

    /// <summary>The PDF/A part based on PDF 1.4.</summary>
    private const int Part1 = 1;

    /// <summary>The PDF/A part based on PDF 2.0.</summary>
    private const int Part4 = 4;

    /// <summary>The table: one row per CMap, a dash where a version does not define it.</summary>
    private const string Table =
        "GB-EUC-H GB1 0 0 5;GB-EUC-V GB1 0 0 5;GBpc-EUC-H GB1 0 0 5;GBpc-EUC-V GB1 0 0 5;GBK-EUC-H GB1 2 2 5;GBK-EUC-V GB1 2 2 5;"
        + "GBKp-EUC-H GB1 2 2 5;GBKp-EUC-V GB1 2 2 5;GBK2K-H GB1 4 4 5;GBK2K-V GB1 4 4 5;UniGB-UCS2-H GB1 4 4 5;UniGB-UCS2-V GB1 4 4 5;"
        + "UniGB-UTF16-H GB1 - 4 5;UniGB-UTF16-V GB1 - 4 5;B5pc-H CNS1 0 0 7;B5pc-V CNS1 0 0 7;HKscs-B5-H CNS1 3 3 7;HKscs-B5-V CNS1 3 3 7;"
        + "ETen-B5-H CNS1 0 0 7;ETen-B5-V CNS1 0 0 7;ETenms-B5-H CNS1 0 0 7;ETenms-B5-V CNS1 0 0 7;CNS-EUC-H CNS1 0 0 7;CNS-EUC-V CNS1 0 0 7;"
        + "UniCNS-UCS2-H CNS1 3 3 7;UniCNS-UCS2-V CNS1 3 3 7;UniCNS-UTF16-H CNS1 - 4 7;UniCNS-UTF16-V CNS1 - 4 7;83pv-RKSJ-H Japan1 1 1 7;"
        + "90ms-RKSJ-H Japan1 2 2 7;90ms-RKSJ-V Japan1 2 2 7;90msp-RKSJ-H Japan1 2 2 7;90msp-RKSJ-V Japan1 2 2 7;90pv-RKSJ-H Japan1 1 1 7;"
        + "Add-RKSJ-H Japan1 1 1 7;Add-RKSJ-V Japan1 1 1 7;EUC-H Japan1 1 1 7;EUC-V Japan1 1 1 7;Ext-RKSJ-H Japan1 2 2 7;Ext-RKSJ-V Japan1 2 2 7;"
        + "H Japan1 1 1 7;V Japan1 1 1 7;UniJIS-UCS2-H Japan1 4 4 7;UniJIS-UCS2-V Japan1 4 4 7;UniJIS-UCS2-HW-H Japan1 4 4 7;"
        + "UniJIS-UCS2-HW-V Japan1 4 4 7;UniJIS-UTF16-H Japan1 - 5 7;UniJIS-UTF16-V Japan1 - 5 7;KSC-EUC-H Korea1 0 0 9;KSC-EUC-V Korea1 0 0 9;"
        + "KSCms-UHC-H Korea1 1 1 9;KSCms-UHC-V Korea1 1 1 9;KSCms-UHC-HW-H Korea1 1 1 9;KSCms-UHC-HW-V Korea1 1 1 9;KSCpc-EUC-H Korea1 0 0 9;"
        + "UniKS-UCS2-H Korea1 1 1 9;UniKS-UCS2-V Korea1 1 1 9;UniKS-UTF16-H Korea1 - 2 9;UniKS-UTF16-V Korea1 - 2 9;"
        + "Identity-H Identity 0 0 0;Identity-V Identity 0 0 0";

    /// <summary>The PDF 2.0 ordering of the Korean collection, which replaced Korea1.</summary>
    private const string Pdf20Korean = "KR";

    /// <summary>The ordering of the Korean collection before PDF 2.0.</summary>
    private const string Korean = "Korea1";

    /// <summary>The rows by CMap name.</summary>
    private static readonly FrozenDictionary<string, string[]> Rows = ReadTable();

    /// <summary>Determines whether a name is a predefined CMap.</summary>
    /// <param name="name">The CMap name.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsPredefined(string name) => Rows.ContainsKey(name);

    /// <summary>Gets the character collection a predefined CMap uses for a PDF/A part.</summary>
    /// <param name="name">The CMap name.</param>
    /// <param name="part">The PDF/A part: 1 reads PDF 1.4, 4 reads PDF 2.0, the others PDF 1.7.</param>
    /// <returns>The collection, or null when the name is not predefined in that version.</returns>
    internal static PdfACMapCollection? Collection(string name, int part)
    {
        if (!Rows.TryGetValue(name, out var row))
        {
            return null;
        }

        var field = part switch
        {
            Part1 => Pdf14Field,
            Part4 => Pdf20Field,
            _ => Pdf17Field,
        };
        if (!int.TryParse(row[field], NumberStyles.Integer, CultureInfo.InvariantCulture, out var supplement))
        {
            return null;
        }

        var ordering = part == Part4 && row[1] == Korean ? Pdf20Korean : row[1];
        return new(ordering, supplement);
    }

    /// <summary>Reads the table.</summary>
    /// <returns>The rows by name.</returns>
    private static FrozenDictionary<string, string[]> ReadTable()
    {
        var rows = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var row in Table.Split(';'))
        {
            var fields = row.Split(' ');
            if (fields.Length == RowFields)
            {
                rows[fields[0]] = fields;
            }
        }

        return rows.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
