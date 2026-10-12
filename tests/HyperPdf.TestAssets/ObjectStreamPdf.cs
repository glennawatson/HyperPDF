// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace HyperPdf.TestAssets;

/// <summary>
/// Writes a document whose page dictionaries, page tree and font live in many small object streams, behind a
/// cross-reference stream. Each page draws its own number as text.
/// </summary>
public static class ObjectStreamPdf
{
    /// <summary>The object number of the catalog.</summary>
    private const int CatalogNumber = 1;

    /// <summary>The object number of the page tree.</summary>
    private const int PagesNumber = 2;

    /// <summary>The object number of the shared font.</summary>
    private const int FontNumber = 3;

    /// <summary>The object number of the first page dictionary.</summary>
    private const int FirstPageNumber = 4;

    /// <summary>The object numbers one page uses: its dictionary and its content stream.</summary>
    private const int ObjectsPerPage = 2;

    /// <summary>The bytes in a cross-reference stream entry (field widths 1, 4 and 2).</summary>
    private const int EntryLength = 7;

    /// <summary>The cross-reference entry type of an object in the file.</summary>
    private const byte InFileType = 1;

    /// <summary>The cross-reference entry type of an object in an object stream.</summary>
    private const byte CompressedType = 2;

    /// <summary>The generation of the free entry that heads the table.</summary>
    private const int FreeGeneration = ushort.MaxValue;

    /// <summary>The number the first page's text shows is its index plus this.</summary>
    private const int FirstPageLabel = 1;

    /// <summary>Gets the number of the first object after the pages' own objects, which is the first object stream.</summary>
    /// <param name="pageCount">The page count.</param>
    /// <returns>The first container's object number.</returns>
    public static int FirstContainer(int pageCount) => FirstPageNumber + (pageCount * ObjectsPerPage);

    /// <summary>Gets the object number of a page's dictionary, which is packed in an object stream.</summary>
    /// <param name="pageIndex">The zero-based page index.</param>
    /// <returns>The object number.</returns>
    public static int PageObject(int pageIndex) => FirstPageNumber + (pageIndex * ObjectsPerPage);

    /// <summary>Gets how many object streams <see cref="Create"/> writes.</summary>
    /// <param name="pageCount">The page count.</param>
    /// <param name="objectsPerStream">The objects packed in each object stream.</param>
    /// <returns>The object stream count.</returns>
    public static int ContainerCount(int pageCount, int objectsPerStream) => (PackedCount(pageCount) + objectsPerStream - 1) / objectsPerStream;

    /// <summary>Writes the document.</summary>
    /// <param name="pageCount">The page count.</param>
    /// <param name="objectsPerStream">The objects packed in each object stream.</param>
    /// <returns>The file.</returns>
    public static byte[] Create(int pageCount, int objectsPerStream)
    {
        var output = new MemoryStream();
        Append(output, "%PDF-1.7\n");
        var offsets = new Dictionary<int, int>();
        WriteObject(output, offsets, CatalogNumber, string.Create(CultureInfo.InvariantCulture, $"<< /Type /Catalog /Pages {PagesNumber} 0 R >>"));
        for (var page = 0; page < pageCount; page++)
        {
            var text = $"BT /F1 24 Tf 72 700 Td (Page {page + FirstPageLabel}) Tj ET";
            WriteStream(output, offsets, PageObject(page) + 1, text);
        }

        var packed = PackedObjects(pageCount);
        var containers = ContainerCount(pageCount, objectsPerStream);
        var first = FirstContainer(pageCount);
        for (var container = 0; container < containers; container++)
        {
            var count = Math.Min(objectsPerStream, packed.Count - (container * objectsPerStream));
            WriteObjectStream(output, offsets, first + container, packed.GetRange(container * objectsPerStream, count));
        }

        return WriteXrefStream(output, offsets, pageCount, objectsPerStream, first + containers);
    }

    /// <summary>Gets how many objects are packed: the page tree, the font and every page dictionary.</summary>
    /// <param name="pageCount">The page count.</param>
    /// <returns>The count.</returns>
    private static int PackedCount(int pageCount) => pageCount + ObjectsPerPage;

    /// <summary>Builds the packed objects in object-number order.</summary>
    /// <param name="pageCount">The page count.</param>
    /// <returns>The object numbers and their text.</returns>
    private static List<PackedObject> PackedObjects(int pageCount)
    {
        var kids = new StringBuilder();
        for (var page = 0; page < pageCount; page++)
        {
            _ = kids.Append(CultureInfo.InvariantCulture, $"{PageObject(page)} 0 R ");
        }

        var packed = new List<PackedObject>(PackedCount(pageCount))
        {
            new(PagesNumber, string.Create(CultureInfo.InvariantCulture, $"<< /Type /Pages /Count {pageCount} /Kids [{kids}] >>")),
            new(FontNumber, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"),
        };
        for (var page = 0; page < pageCount; page++)
        {
            var number = PageObject(page);
            var resources = string.Create(CultureInfo.InvariantCulture, $"<< /Font << /F1 {FontNumber} 0 R >> >>");
            var body = string.Create(CultureInfo.InvariantCulture, $"<< /Type /Page /Parent {PagesNumber} 0 R /MediaBox [0 0 612 792] /Contents {number + 1} 0 R /Resources {resources} >>");
            packed.Add(new(number, body));
        }

        return packed;
    }

    /// <summary>Writes a plain object.</summary>
    /// <param name="output">The file so far.</param>
    /// <param name="offsets">Where each object was written.</param>
    /// <param name="number">The object number.</param>
    /// <param name="body">The object text.</param>
    private static void WriteObject(MemoryStream output, Dictionary<int, int> offsets, int number, string body)
    {
        offsets[number] = (int)output.Length;
        Append(output, string.Create(CultureInfo.InvariantCulture, $"{number} 0 obj\n{body}\nendobj\n"));
    }

    /// <summary>Writes an unfiltered content stream.</summary>
    /// <param name="output">The file so far.</param>
    /// <param name="offsets">Where each object was written.</param>
    /// <param name="number">The object number.</param>
    /// <param name="data">The stream text.</param>
    private static void WriteStream(MemoryStream output, Dictionary<int, int> offsets, int number, string data)
    {
        offsets[number] = (int)output.Length;
        Append(output, string.Create(CultureInfo.InvariantCulture, $"{number} 0 obj\n<< /Length {data.Length} >>\nstream\n{data}\nendstream\nendobj\n"));
    }

    /// <summary>Writes an unfiltered object stream.</summary>
    /// <param name="output">The file so far.</param>
    /// <param name="offsets">Where each object was written.</param>
    /// <param name="number">The object stream's number.</param>
    /// <param name="objects">The packed objects.</param>
    private static void WriteObjectStream(MemoryStream output, Dictionary<int, int> offsets, int number, List<PackedObject> objects)
    {
        var header = new StringBuilder();
        var content = new StringBuilder();
        foreach (var (objectNumber, body) in objects)
        {
            _ = header.Append(CultureInfo.InvariantCulture, $"{objectNumber} {content.Length} ");
            _ = content.Append(body).Append('\n');
        }

        _ = header.Append('\n');
        var data = header.ToString() + content;
        offsets[number] = (int)output.Length;
        var dictionary = string.Create(CultureInfo.InvariantCulture, $"<< /Type /ObjStm /N {objects.Count} /First {header.Length} /Length {data.Length} >>");
        Append(output, string.Create(CultureInfo.InvariantCulture, $"{number} 0 obj\n{dictionary}\nstream\n{data}\nendstream\nendobj\n"));
    }

    /// <summary>Writes the cross-reference stream and the end of the file.</summary>
    /// <param name="output">The file so far.</param>
    /// <param name="offsets">Where each plain object was written.</param>
    /// <param name="pageCount">The page count.</param>
    /// <param name="objectsPerStream">The objects packed in each object stream.</param>
    /// <param name="xrefNumber">The cross-reference stream's number.</param>
    /// <returns>The file.</returns>
    private static byte[] WriteXrefStream(MemoryStream output, Dictionary<int, int> offsets, int pageCount, int objectsPerStream, int xrefNumber)
    {
        var size = xrefNumber + 1;
        var start = (int)output.Length;
        var data = new byte[size * EntryLength];
        var first = FirstContainer(pageCount);
        WriteEntry(data, 0, 0, 0, FreeGeneration);
        for (var number = 1; number < xrefNumber; number++)
        {
            if (offsets.TryGetValue(number, out var offset))
            {
                WriteEntry(data, number, InFileType, offset, 0);
                continue;
            }

            var slot = PackedSlot(number);
            WriteEntry(data, number, CompressedType, first + (slot / objectsPerStream), slot % objectsPerStream);
        }

        WriteEntry(data, xrefNumber, InFileType, start, 0);
        var dictionary = string.Create(CultureInfo.InvariantCulture, $"<< /Type /XRef /Size {size} /W [1 4 2] /Root {CatalogNumber} 0 R /Length {data.Length} >>");
        Append(output, string.Create(CultureInfo.InvariantCulture, $"{xrefNumber} 0 obj\n{dictionary}\nstream\n"));
        output.Write(data);
        Append(output, string.Create(CultureInfo.InvariantCulture, $"\nendstream\nendobj\nstartxref\n{start}\n%%EOF\n"));
        return output.ToArray();
    }

    /// <summary>Gets a packed object's position in the packing order: the page tree, the font, then the pages.</summary>
    /// <param name="number">The object number.</param>
    /// <returns>The position.</returns>
    private static int PackedSlot(int number) => number switch
    {
        PagesNumber => 0,
        FontNumber => 1,
        _ => ObjectsPerPage + ((number - FirstPageNumber) / ObjectsPerPage),
    };

    /// <summary>Writes one cross-reference stream entry.</summary>
    /// <param name="data">The table.</param>
    /// <param name="number">The object number.</param>
    /// <param name="type">The entry type.</param>
    /// <param name="second">The offset, or the object stream's number.</param>
    /// <param name="third">The generation, or the index in the object stream.</param>
    private static void WriteEntry(byte[] data, int number, byte type, int second, int third)
    {
        var entry = data.AsSpan(number * EntryLength, EntryLength);
        entry[0] = type;
        BinaryPrimitives.WriteInt32BigEndian(entry[1..], second);
        BinaryPrimitives.WriteUInt16BigEndian(entry[(1 + sizeof(int))..], (ushort)third);
    }

    /// <summary>Writes text, one byte per character.</summary>
    /// <param name="output">The file.</param>
    /// <param name="text">The text.</param>
    private static void Append(MemoryStream output, string text) => output.Write(Encoding.Latin1.GetBytes(text));

    /// <summary>An object to pack.</summary>
    /// <param name="Number">The object number.</param>
    /// <param name="Body">The object text.</param>
    private readonly record struct PackedObject(int Number, string Body);
}
