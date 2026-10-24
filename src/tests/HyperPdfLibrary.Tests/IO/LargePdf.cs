// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;

namespace HyperPdfLibrary.Tests.IO;

/// <summary>
/// Writes large PDF files straight to disk without building them in memory: many pages, each with its own big content
/// stream, optionally after a gap that pushes every object past the 2 GB mark (a sparse hole on file systems that support
/// it).
/// </summary>
internal static class LargePdf
{
    /// <summary>The objects before the first page: the catalog and the page tree.</summary>
    private const int FixedObjects = 2;

    /// <summary>The objects per page: the page and its content stream.</summary>
    private const int ObjectsPerPage = 2;

    /// <summary>The write buffer size.</summary>
    private const int BufferSize = 1 << 16;

    /// <summary>Gets the content repeated to fill each page's stream: an empty save and restore.</summary>
    private static ReadOnlySpan<byte> Filler => "q Q\n"u8;

    /// <summary>Writes a file.</summary>
    /// <param name="path">The new file's path.</param>
    /// <param name="pages">The number of pages.</param>
    /// <param name="contentLength">The length of each page's content stream.</param>
    /// <param name="gap">The offset of the first object; zero to follow the header.</param>
    internal static void Write(string path, int pages, int contentLength, long gap)
    {
        var offsets = new long[FixedObjects + (pages * ObjectsPerPage)];
        var content = new byte[contentLength];
        for (var i = 0; i < content.Length; i++)
        {
            content[i] = Filler[i % Filler.Length];
        }

        using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize);
        Ascii(file, "%PDF-1.7\n");
        file.Position = Math.Max(file.Position, gap);
        offsets[0] = file.Position;
        Ascii(file, "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
        offsets[1] = file.Position;
        Ascii(file, PageTree(pages));
        for (var page = 0; page < pages; page++)
        {
            var number = FixedObjects + (page * ObjectsPerPage) + 1;
            offsets[number - 1] = file.Position;
            Ascii(file, string.Create(CultureInfo.InvariantCulture, $"{number} 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents {number + 1} 0 R >>\nendobj\n"));
            offsets[number] = file.Position;
            Ascii(file, string.Create(CultureInfo.InvariantCulture, $"{number + 1} 0 obj\n<< /Length {contentLength} >>\nstream\n"));
            file.Write(content);
            Ascii(file, "\nendstream\nendobj\n");
        }

        WriteTable(file, offsets);
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
    private static void WriteTable(FileStream file, long[] offsets)
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

    /// <summary>Writes ASCII text.</summary>
    /// <param name="file">The file.</param>
    /// <param name="text">The text.</param>
    private static void Ascii(FileStream file, string text) => file.Write(Encoding.ASCII.GetBytes(text));
}
