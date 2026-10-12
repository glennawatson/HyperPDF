// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;

namespace HyperPdfLibrary.Tests.Conformance;

/// <summary>
/// Writes a small PDF byte for byte, so tests can break exactly one piece of file syntax: a catalog, one page and its
/// content stream, plus any extra objects, with a classic cross-reference table and a trailer /ID.
/// </summary>
internal sealed class StructureTestPdf
{
    /// <summary>The object number of the first extra object.</summary>
    internal const int FirstExtra = 5;

    /// <summary>Gets or sets the header line.</summary>
    internal string Header { get; set; } = "%PDF-1.7";

    /// <summary>Gets or sets the comment line after the header; four bytes above 127 by default.</summary>
    internal string Comment { get; set; } = "%âãÏÓ";

    /// <summary>Gets or sets the extra catalog entries.</summary>
    internal string CatalogEntries { get; set; } = string.Empty;

    /// <summary>Gets or sets the page's content stream.</summary>
    internal string Content { get; set; } = "0 0 1 1 re f";

    /// <summary>Gets or sets the extra trailer entries; a file identifier by default.</summary>
    internal string TrailerEntries { get; set; } = " /ID [<0123456789ABCDEF0123456789ABCDEF> <0123456789ABCDEF0123456789ABCDEF>]";

    /// <summary>Gets or sets the bytes written after the last end-of-file marker.</summary>
    internal string AfterEnd { get; set; } = "\n";

    /// <summary>Gets or sets the header of every object, given its number; written as <c>N 0 obj</c> on its own line by default.</summary>
    internal Func<int, string> ObjectHeader { get; set; } = static number => string.Create(CultureInfo.InvariantCulture, $"{number} 0 obj\n");

    /// <summary>Gets the extra objects' bodies, numbered from <see cref="FirstExtra"/>.</summary>
    internal List<string> Extra { get; } = [];

    /// <summary>Makes a stream object body with a correct /Length.</summary>
    /// <param name="entries">The extra dictionary entries.</param>
    /// <param name="data">The data.</param>
    /// <returns>The body.</returns>
    internal static string Stream(string entries, string data) =>
        string.Create(CultureInfo.InvariantCulture, $"<< /Length {Encoding.Latin1.GetByteCount(data)}{entries} >>\nstream\n{data}\nendstream");

    /// <summary>Writes the file.</summary>
    /// <returns>The bytes.</returns>
    internal byte[] Build()
    {
        string[] bodies =
        [
            $"<< /Type /Catalog /Pages 2 0 R{CatalogEntries} >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 10 10] /Contents 4 0 R >>",
            Stream(string.Empty, Content),
            .. Extra,
        ];
        var text = new StringBuilder();
        _ = text.Append(Header).Append('\n').Append(Comment).Append('\n');
        var offsets = new int[bodies.Length];
        for (var i = 0; i < bodies.Length; i++)
        {
            offsets[i] = Encoding.Latin1.GetByteCount(text.ToString());
            _ = text.Append(ObjectHeader(i + 1)).Append(bodies[i]).Append("\nendobj\n");
        }

        var xref = Encoding.Latin1.GetByteCount(text.ToString());
        _ = text.Append(CultureInfo.InvariantCulture, $"xref\n0 {bodies.Length + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            _ = text.Append(CultureInfo.InvariantCulture, $"{offset:D10} 00000 n \n");
        }

        _ = text.Append(CultureInfo.InvariantCulture, $"trailer\n<< /Size {bodies.Length + 1} /Root 1 0 R{TrailerEntries} >>\nstartxref\n{xref}\n%%EOF").Append(AfterEnd);
        return Encoding.Latin1.GetBytes(text.ToString());
    }
}
