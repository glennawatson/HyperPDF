// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;

namespace HyperPdfLibrary.Tests.Security;

/// <summary>Writes indirect objects and records where each one starts, counting from a chosen file offset.</summary>
internal sealed class PdfBytes
{
    /// <summary>The bytes written.</summary>
    private readonly MemoryStream _output = new();

    /// <summary>The file offset of each object, by number.</summary>
    private readonly Dictionary<int, int> _offsets = [];

    /// <summary>The file offset of the first byte this builder writes.</summary>
    private readonly int _origin;

    /// <summary>Initializes a new instance of the <see cref="PdfBytes"/> class that starts at offset zero.</summary>
    internal PdfBytes()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="PdfBytes"/> class that continues a file.</summary>
    /// <param name="origin">The length of the file so far.</param>
    internal PdfBytes(int origin) => _origin = origin;

    /// <summary>Writes text, one byte per character.</summary>
    /// <param name="text">The text.</param>
    internal void Raw(string text) => _output.Write(Encoding.Latin1.GetBytes(text));

    /// <summary>Writes a non-stream object.</summary>
    /// <param name="number">The object number.</param>
    /// <param name="body">The object text.</param>
    internal void Object(int number, string body)
    {
        _offsets[number] = _origin + (int)_output.Length;
        Raw(string.Create(CultureInfo.InvariantCulture, $"{number} 0 obj\n{body}\nendobj\n"));
    }

    /// <summary>Writes a stream object with the right /Length.</summary>
    /// <param name="number">The object number.</param>
    /// <param name="data">The stream bytes as stored.</param>
    internal void Stream(int number, byte[] data)
    {
        _offsets[number] = _origin + (int)_output.Length;
        Raw(string.Create(CultureInfo.InvariantCulture, $"{number} 0 obj\n<< /Length {data.Length} >>\nstream\n"));
        _output.Write(data);
        Raw("\nendstream\nendobj\n");
    }

    /// <summary>Gets the file offset where an object was written.</summary>
    /// <param name="number">The object number.</param>
    /// <returns>The offset.</returns>
    internal int OffsetOf(int number) => _offsets[number];

    /// <summary>Writes the classic table entries of a run of objects.</summary>
    /// <param name="first">The first object number.</param>
    /// <param name="last">The last object number.</param>
    /// <returns>The entries, 20 bytes each.</returns>
    internal string Entries(int first, int last) =>
        string.Concat(Enumerable.Range(first, last - first + 1).Select(number => string.Create(CultureInfo.InvariantCulture, $"{_offsets[number]:D10} 00000 n \n")));

    /// <summary>Gets the bytes written.</summary>
    /// <returns>The bytes.</returns>
    internal byte[] ToArray() => _output.ToArray();
}
