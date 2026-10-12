// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;

namespace HyperPdf.TestAssets;

/// <summary>Builds PDFs whose pages hold a lot of uncompressed drawing, so opening and rendering them takes real CPU time.</summary>
public static class HeavyPdf
{
    /// <summary>The objects before the first page: the catalog and the page tree.</summary>
    private const int FixedObjects = 2;

    /// <summary>The objects per page: the page and its content stream.</summary>
    private const int ObjectsPerPage = 2;

    /// <summary>The width of the pages in points.</summary>
    private const int PageWidth = 612;

    /// <summary>The height of the pages in points.</summary>
    private const int PageHeight = 792;

    /// <summary>Creates a file with pages that each draw many short lines.</summary>
    /// <param name="pages">The number of pages.</param>
    /// <param name="contentLength">The length of each page's content stream in bytes.</param>
    /// <returns>The PDF bytes.</returns>
    public static byte[] Create(int pages, int contentLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pages);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(contentLength);
        var content = Fill(contentLength);
        using var file = new MemoryStream();
        var offsets = new long[FixedObjects + (pages * ObjectsPerPage)];
        Ascii(file, "%PDF-1.7\n");
        offsets[0] = file.Position;
        Ascii(file, "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
        offsets[1] = file.Position;
        Ascii(file, PageTree(pages));
        for (var page = 0; page < pages; page++)
        {
            var number = FixedObjects + (page * ObjectsPerPage) + 1;
            offsets[number - 1] = file.Position;
            Ascii(file, string.Create(CultureInfo.InvariantCulture, $"{number} 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {PageWidth} {PageHeight}] /Contents {number + 1} 0 R >>\nendobj\n"));
            offsets[number] = file.Position;
            Ascii(file, string.Create(CultureInfo.InvariantCulture, $"{number + 1} 0 obj\n<< /Length {content.Length} >>\nstream\n"));
            file.Write(content);
            Ascii(file, "\nendstream\nendobj\n");
        }

        WriteTable(file, offsets);
        return file.ToArray();
    }

    /// <summary>Builds a page's content: many stroked lines, each in its own saved state.</summary>
    /// <param name="length">The length in bytes.</param>
    /// <returns>The content.</returns>
    private static byte[] Fill(int length)
    {
        var line = "q 1 0 0 RG 10 10 m 300 700 l S Q\n"u8;
        var content = new byte[length];
        for (var offset = 0; offset < length; offset += line.Length)
        {
            line[..Math.Min(line.Length, length - offset)].CopyTo(content.AsSpan(offset));
        }

        return content;
    }

    /// <summary>Writes the page tree object.</summary>
    /// <param name="pages">The number of pages.</param>
    /// <returns>The object text.</returns>
    private static string PageTree(int pages)
    {
        var tree = new StringBuilder("2 0 obj\n<< /Type /Pages /Kids [");
        for (var page = 0; page < pages; page++)
        {
            _ = tree.Append(CultureInfo.InvariantCulture, $"{FixedObjects + (page * ObjectsPerPage) + 1} 0 R ");
        }

        return tree.Append(CultureInfo.InvariantCulture, $"] /Count {pages} >>\nendobj\n").ToString();
    }

    /// <summary>Writes the cross-reference table, trailer and <c>startxref</c>.</summary>
    /// <param name="file">The file.</param>
    /// <param name="offsets">The offset of each object.</param>
    private static void WriteTable(MemoryStream file, long[] offsets)
    {
        var start = file.Position;
        var table = new StringBuilder();
        _ = table.Append(CultureInfo.InvariantCulture, $"xref\n0 {offsets.Length + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            _ = table.Append(CultureInfo.InvariantCulture, $"{offset:D10} 00000 n \n");
        }

        _ = table.Append(CultureInfo.InvariantCulture, $"trailer\n<< /Size {offsets.Length + 1} /Root 1 0 R >>\nstartxref\n{start}\n%%EOF\n");
        Ascii(file, table.ToString());
    }

    /// <summary>Writes text as ASCII.</summary>
    /// <param name="file">The file.</param>
    /// <param name="text">The text.</param>
    private static void Ascii(MemoryStream file, string text) => file.Write(Encoding.ASCII.GetBytes(text));
}
