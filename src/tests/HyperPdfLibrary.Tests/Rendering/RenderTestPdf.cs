// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>Builds a one page PDF with known content for the rendering tests.</summary>
[DebuggerDisplay("RenderTestPdf: {Width}x{Height}")]
internal sealed class RenderTestPdf
{
    /// <summary>The object number of the catalog.</summary>
    private const int CatalogNumber = 1;

    /// <summary>The object number of the page tree.</summary>
    private const int PagesNumber = 2;

    /// <summary>The object number of the page.</summary>
    private const int PageNumber = 3;

    /// <summary>The object number of the page content.</summary>
    private const int ContentNumber = 4;

    /// <summary>The objects added by tests, numbered from the first free number.</summary>
    private readonly List<byte[]> _extra = [];

    /// <summary>Initializes a new instance of the <see cref="RenderTestPdf"/> class.</summary>
    /// <param name="width">The page width in points.</param>
    /// <param name="height">The page height in points.</param>
    internal RenderTestPdf(int width, int height)
    {
        Width = width;
        Height = height;
    }

    /// <summary>Gets the page width in points.</summary>
    internal int Width { get; }

    /// <summary>Gets the page height in points.</summary>
    internal int Height { get; }

    /// <summary>Gets or sets the page content stream.</summary>
    internal string Content { get; set; } = string.Empty;

    /// <summary>Gets or sets the entries of the page /Resources dictionary.</summary>
    internal string Resources { get; set; } = string.Empty;

    /// <summary>Gets or sets extra entries of the page dictionary, such as /Rotate or /Annots.</summary>
    internal string PageEntries { get; set; } = string.Empty;

    /// <summary>Gets or sets extra entries of the catalog dictionary, such as /OCProperties.</summary>
    internal string CatalogEntries { get; set; } = string.Empty;

    /// <summary>Gets or sets extra entries of the trailer dictionary, such as /Info.</summary>
    internal string TrailerEntries { get; set; } = string.Empty;

    /// <summary>Gets or sets the PDF version the header names, such as 1.7 or 2.0.</summary>
    internal string Version { get; set; } = "1.7";

    /// <summary>Gets the number the next added object receives.</summary>
    internal int NextNumber => ContentNumber + _extra.Count + 1;

    /// <summary>Adds an object.</summary>
    /// <param name="body">The object's text, such as a dictionary.</param>
    /// <returns>The object number.</returns>
    internal int AddObject(string body)
    {
        _extra.Add(Encoding.ASCII.GetBytes(body));
        return ContentNumber + _extra.Count;
    }

    /// <summary>Adds a stream object with text data.</summary>
    /// <param name="entries">The dictionary entries other than /Length.</param>
    /// <param name="data">The stream data.</param>
    /// <returns>The object number.</returns>
    internal int AddStream(string entries, string data) => AddStream(entries, Encoding.ASCII.GetBytes(data));

    /// <summary>Adds a stream object.</summary>
    /// <param name="entries">The dictionary entries other than /Length.</param>
    /// <param name="data">The stream data.</param>
    /// <returns>The object number.</returns>
    internal int AddStream(string entries, byte[] data)
    {
        using var buffer = new MemoryStream();
        Write(buffer, string.Create(CultureInfo.InvariantCulture, $"<< {entries} /Length {data.Length} >>\nstream\n"));
        buffer.Write(data);
        Write(buffer, "\nendstream");
        _extra.Add(buffer.ToArray());
        return ContentNumber + _extra.Count;
    }

    /// <summary>Writes the PDF.</summary>
    /// <returns>The file's bytes.</returns>
    internal byte[] ToBytes()
    {
        using var file = new MemoryStream();
        Write(file, $"%PDF-{Version}\n");
        var offsets = new List<long>();
        var content = Encoding.ASCII.GetBytes(Content);
        WriteObject(file, offsets, CatalogNumber, Encoding.ASCII.GetBytes(string.Create(CultureInfo.InvariantCulture, $"<< /Type /Catalog /Pages {PagesNumber} 0 R {CatalogEntries} >>")));
        WriteObject(file, offsets, PagesNumber, Encoding.ASCII.GetBytes(string.Create(CultureInfo.InvariantCulture, $"<< /Type /Pages /Kids [{PageNumber} 0 R] /Count 1 >>")));
        var page = string.Create(
            CultureInfo.InvariantCulture,
            $"<< /Type /Page /Parent {PagesNumber} 0 R /MediaBox [0 0 {Width} {Height}] /Resources << {Resources} >> /Contents {ContentNumber} 0 R {PageEntries} >>");
        WriteObject(file, offsets, PageNumber, Encoding.ASCII.GetBytes(page));
        using (var stream = new MemoryStream())
        {
            Write(stream, string.Create(CultureInfo.InvariantCulture, $"<< /Length {content.Length} >>\nstream\n"));
            stream.Write(content);
            Write(stream, "\nendstream");
            WriteObject(file, offsets, ContentNumber, stream.ToArray());
        }

        for (var i = 0; i < _extra.Count; i++)
        {
            WriteObject(file, offsets, ContentNumber + 1 + i, _extra[i]);
        }

        var tableOffset = file.Position;
        Write(file, string.Create(CultureInfo.InvariantCulture, $"xref\n0 {offsets.Count + 1}\n0000000000 65535 f \n"));
        foreach (var offset in offsets)
        {
            Write(file, string.Create(CultureInfo.InvariantCulture, $"{offset:D10} 00000 n \n"));
        }

        Write(file, string.Create(CultureInfo.InvariantCulture, $"trailer\n<< /Size {offsets.Count + 1} /Root {CatalogNumber} 0 R {TrailerEntries} >>\nstartxref\n{tableOffset}\n%%EOF\n"));
        return file.ToArray();
    }

    /// <summary>Writes text.</summary>
    /// <param name="stream">The destination.</param>
    /// <param name="text">The ASCII text.</param>
    private static void Write(MemoryStream stream, string text) => stream.Write(Encoding.ASCII.GetBytes(text));

    /// <summary>Writes an indirect object and records its offset.</summary>
    /// <param name="file">The destination.</param>
    /// <param name="offsets">Receives the offset.</param>
    /// <param name="number">The object number.</param>
    /// <param name="body">The object's bytes.</param>
    private static void WriteObject(MemoryStream file, List<long> offsets, int number, byte[] body)
    {
        offsets.Add(file.Position);
        Write(file, string.Create(CultureInfo.InvariantCulture, $"{number} 0 obj\n"));
        file.Write(body);
        Write(file, "\nendobj\n");
    }
}
