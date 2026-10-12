// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;

namespace HyperPdf.TestAssets;

/// <summary>Builds small PDF files from object bodies written as text, for tests that need exact (often damaged) structures.</summary>
public static class MiniPdf
{
    /// <summary>Builds a PDF file whose object <c>n</c> is <c>objects[n - 1]</c>. Object 1 must be the catalog.</summary>
    /// <param name="objects">The object bodies, such as <c>&lt;&lt; /Type /Catalog /Pages 2 0 R &gt;&gt;</c>.</param>
    /// <returns>The file bytes; the cross-reference table is correct.</returns>
    public static byte[] Build(params string[] objects)
    {
        ArgumentNullException.ThrowIfNull(objects);
        var latin1 = Encoding.Latin1;
        var output = new StringBuilder("%PDF-1.7\n");
        var offsets = new int[objects.Length];
        for (var i = 0; i < objects.Length; i++)
        {
            offsets[i] = latin1.GetByteCount(output.ToString());
            _ = output.Append(CultureInfo.InvariantCulture, $"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }

        var xref = latin1.GetByteCount(output.ToString());
        _ = output.Append(CultureInfo.InvariantCulture, $"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            _ = output.Append(CultureInfo.InvariantCulture, $"{offset:D10} 00000 n \n");
        }

        _ = output.Append(CultureInfo.InvariantCulture, $"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return latin1.GetBytes(output.ToString());
    }

    /// <summary>Writes a stream object body with the right /Length.</summary>
    /// <param name="entries">Extra dictionary entries, such as <c>/Subtype /Form</c>.</param>
    /// <param name="data">The stream data; one byte per character.</param>
    /// <returns>The object body.</returns>
    public static string Stream(string entries, string data)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(data);
        return string.Create(CultureInfo.InvariantCulture, $"<< {entries} /Length {data.Length} >>\nstream\n{data}\nendstream");
    }
}
