// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace HyperPdfLibrary.Tests.Syntax;

/// <summary>Builds PDF files object by object, recording offsets so a test can write a correct, wrong or missing table.</summary>
internal sealed class RawPdf
{
    /// <summary>The bytes written so far.</summary>
    private readonly MemoryStream _output = new();

    /// <summary>The offset of each object, by number.</summary>
    private readonly Dictionary<int, int> _offsets = [];

    /// <summary>Initializes a new instance of the <see cref="RawPdf"/> class holding only the header.</summary>
    internal RawPdf() => Append("%PDF-1.7\n");

    /// <summary>Gets the number of bytes written.</summary>
    internal int Position => (int)_output.Length;

    /// <summary>Gets the offset where an object was written.</summary>
    /// <param name="number">The object number.</param>
    /// <returns>The offset.</returns>
    internal int OffsetOf(int number) => _offsets[number];

    /// <summary>Writes text, one byte per character.</summary>
    /// <param name="text">The text.</param>
    /// <returns>This builder.</returns>
    internal RawPdf Append(string text) => Append(Encoding.Latin1.GetBytes(text));

    /// <summary>Writes bytes.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>This builder.</returns>
    internal RawPdf Append(ReadOnlySpan<byte> bytes)
    {
        _output.Write(bytes);
        return this;
    }

    /// <summary>Writes a non-stream object.</summary>
    /// <param name="number">The object number.</param>
    /// <param name="body">The object text.</param>
    /// <returns>This builder.</returns>
    internal RawPdf Object(int number, string body)
    {
        _offsets[number] = Position;
        return Append(string.Create(CultureInfo.InvariantCulture, $"{number} 0 obj\n{body}\nendobj\n"));
    }

    /// <summary>Writes a stream object with the right /Length.</summary>
    /// <param name="number">The object number.</param>
    /// <param name="entries">Dictionary entries other than /Length.</param>
    /// <param name="data">The stream bytes as stored.</param>
    /// <returns>This builder.</returns>
    internal RawPdf Stream(int number, string entries, ReadOnlySpan<byte> data)
    {
        _offsets[number] = Position;
        _ = Append(string.Create(CultureInfo.InvariantCulture, $"{number} 0 obj\n<< {entries} /Length {data.Length} >>\nstream\n"));
        _ = Append(data);
        return Append("\nendstream\nendobj\n");
    }

    /// <summary>Writes an object stream whose objects are given as text.</summary>
    /// <param name="number">The object stream's number.</param>
    /// <param name="numbers">The numbers of the packed objects.</param>
    /// <param name="bodies">The packed objects' text, in the same order.</param>
    /// <returns>This builder.</returns>
    internal RawPdf ObjectStream(int number, int[] numbers, string[] bodies) => ObjectStream(number, numbers, bodies, static data => data);

    /// <summary>Writes an object stream, passing its data through a function first so a test can encrypt it.</summary>
    /// <param name="number">The object stream's number.</param>
    /// <param name="numbers">The numbers of the packed objects.</param>
    /// <param name="bodies">The packed objects' text, in the same order.</param>
    /// <param name="encrypt">Turns the plain stream data into the bytes to store.</param>
    /// <returns>This builder.</returns>
    internal RawPdf ObjectStream(int number, int[] numbers, string[] bodies, Func<byte[], byte[]> encrypt)
    {
        var entries = ObjectStreamEntries(numbers, bodies, out var data);
        return Stream(number, $"/Type /ObjStm {entries}", encrypt(data));
    }

    /// <summary>Writes a classic table with the real offsets, then the trailer and <c>startxref</c>.</summary>
    /// <param name="size">The /Size.</param>
    /// <param name="trailerEntries">Trailer entries such as <c>/Root 1 0 R</c>.</param>
    /// <returns>This builder.</returns>
    internal RawPdf Table(int size, string trailerEntries) => Table(size, trailerEntries, []);

    /// <summary>Writes a classic table in which chosen entries point at the wrong place.</summary>
    /// <param name="size">The /Size.</param>
    /// <param name="trailerEntries">Trailer entries such as <c>/Root 1 0 R</c>.</param>
    /// <param name="wrongOffsets">Offsets to write instead of the real ones, by object number.</param>
    /// <returns>This builder.</returns>
    internal RawPdf Table(int size, string trailerEntries, Dictionary<int, int> wrongOffsets)
    {
        var start = Position;
        _ = Append(string.Create(CultureInfo.InvariantCulture, $"xref\n0 {size}\n0000000000 65535 f \n"));
        for (var number = 1; number < size; number++)
        {
            var offset = wrongOffsets.TryGetValue(number, out var wrong) ? wrong : _offsets.GetValueOrDefault(number);
            _ = Append(string.Create(CultureInfo.InvariantCulture, $"{offset:D10} 00000 n \n"));
        }

        return Append(string.Create(CultureInfo.InvariantCulture, $"trailer\n<< /Size {size} {trailerEntries} >>\nstartxref\n{start}\n%%EOF\n"));
    }

    /// <summary>Writes an unfiltered cross-reference stream and <c>startxref</c>.</summary>
    /// <param name="number">The cross-reference stream's object number.</param>
    /// <param name="size">The /Size.</param>
    /// <param name="index">The /Index array text, such as <c>[0 4]</c>.</param>
    /// <param name="trailerEntries">Entries such as <c>/Root 1 0 R</c>.</param>
    /// <param name="entries">The entries in order.</param>
    /// <returns>This builder.</returns>
    internal RawPdf XrefStream(int number, int size, string index, string trailerEntries, IReadOnlyList<XrefStreamEntry> entries)
    {
        var start = Position;
        var data = new byte[entries.Count * XrefStreamEntry.Length];
        for (var i = 0; i < entries.Count; i++)
        {
            entries[i].WriteTo(data.AsSpan(i * XrefStreamEntry.Length));
        }

        _ = Stream(number, string.Create(CultureInfo.InvariantCulture, $"/Type /XRef /Size {size} /Index {index} /W [1 4 2] {trailerEntries}"), data);
        return Append(string.Create(CultureInfo.InvariantCulture, $"startxref\n{start}\n%%EOF\n"));
    }

    /// <summary>Gets the file.</summary>
    /// <returns>The bytes written.</returns>
    internal byte[] ToArray() => _output.ToArray();

    /// <summary>Builds an object stream's header and data.</summary>
    /// <param name="numbers">The packed object numbers.</param>
    /// <param name="bodies">The packed object text.</param>
    /// <param name="data">The stream data.</param>
    /// <returns>The /N and /First entries.</returns>
    private static string ObjectStreamEntries(int[] numbers, string[] bodies, out byte[] data)
    {
        var header = new StringBuilder();
        var content = new StringBuilder();
        for (var i = 0; i < numbers.Length; i++)
        {
            _ = header.Append(CultureInfo.InvariantCulture, $"{numbers[i]} {content.Length} ");
            _ = content.Append(bodies[i]).Append('\n');
        }

        _ = header.Append('\n');
        var first = header.Length;
        data = Encoding.Latin1.GetBytes(header.Append(content).ToString());
        return string.Create(CultureInfo.InvariantCulture, $"/N {numbers.Length} /First {first}");
    }

    /// <summary>One cross-reference stream entry with fields of 1, 4 and 2 bytes.</summary>
    /// <param name="Type">The entry type: 0 free, 1 in the file, 2 compressed.</param>
    /// <param name="Second">The offset, or the object stream's number.</param>
    /// <param name="Third">The generation, or the index in the object stream.</param>
    internal readonly record struct XrefStreamEntry(byte Type, int Second, int Third)
    {
        /// <summary>The bytes in an entry.</summary>
        internal const int Length = 7;

        /// <summary>Writes the entry.</summary>
        /// <param name="destination">The seven bytes.</param>
        internal void WriteTo(Span<byte> destination)
        {
            destination[0] = Type;
            BinaryPrimitives.WriteInt32BigEndian(destination[1..], Second);
            BinaryPrimitives.WriteUInt16BigEndian(destination[(1 + sizeof(int))..], (ushort)Third);
        }
    }
}
