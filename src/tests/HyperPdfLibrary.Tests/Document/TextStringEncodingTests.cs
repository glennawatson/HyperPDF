// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Document;

/// <summary>Tests how text strings are encoded when written, and that existing strings survive a save.</summary>
public sealed class TextStringEncodingTests
{
    /// <summary>The header version of a PDF 1.7 document.</summary>
    private const string Version17 = "1.7";

    /// <summary>The header version of a PDF 2.0 document.</summary>
    private const string Version20 = "2.0";

    /// <summary>The widget index of the text field on the first page.</summary>
    private const int FieldIndex = 1;

    /// <summary>The PDFDocEncoding byte of the euro sign.</summary>
    private const byte EuroCode = 0xA0;

    /// <summary>The PDFDocEncoding byte of the bullet.</summary>
    private const byte BulletCode = 0x80;

    /// <summary>The Latin-1 byte of e with an acute accent.</summary>
    private const byte EAcuteCode = 0xE9;

    /// <summary>The text used by existing UTF-16 strings, as a hex string body.</summary>
    private const string CafeUtf16Hex = "FEFF00430061006600E9";

    /// <summary>The text used by existing UTF-8 strings, as a hex string body.</summary>
    private const string CafeUtf8Hex = "EFBBBF436166C3A9";

    /// <summary>Text that PDFDocEncoding cannot hold.</summary>
    private const string Omega = "Ωmega";

    /// <summary>The UTF-16 big-endian byte order mark.</summary>
    private static readonly byte[] Utf16Mark = [0xFE, 0xFF];

    /// <summary>The UTF-8 byte order mark.</summary>
    private static readonly byte[] Utf8Mark = [0xEF, 0xBB, 0xBF];

    /// <summary>The PDFDocEncoding bytes of a euro sign.</summary>
    private static readonly byte[] EuroBytes = [EuroCode];

    /// <summary>The PDFDocEncoding bytes of a bullet.</summary>
    private static readonly byte[] BulletBytes = [BulletCode];

    /// <summary>The Latin-1 bytes of e with an acute accent.</summary>
    private static readonly byte[] EAcuteBytes = [EAcuteCode];

    /// <summary>Gets the UTF-16 big-endian bytes of "Café".</summary>
    private static byte[] CafeUtf16 => Convert.FromHexString(CafeUtf16Hex);

    /// <summary>Gets the UTF-8 bytes of "Café" with a byte order mark.</summary>
    private static byte[] CafeUtf8 => Convert.FromHexString(CafeUtf8Hex);

    /// <summary>Text that fits PDFDocEncoding is stored as PDFDocEncoding, with the special codes mapped.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FittingTextIsPdfDocEncoded()
    {
        await Assert.That(PdfText.Encode("Cafe")).IsEquivalentTo("Cafe"u8.ToArray());
        await Assert.That(PdfText.Encode("é", Version20)).IsEquivalentTo(EAcuteBytes);
        await Assert.That(PdfText.Encode("€")).IsEquivalentTo(EuroBytes);
        await Assert.That(PdfText.Encode("•")).IsEquivalentTo(BulletBytes);
        await Assert.That(PdfText.Decode(PdfText.Encode("€•é", Version17))).IsEqualTo("€•é");
    }

    /// <summary>Text outside PDFDocEncoding is UTF-16BE before PDF 2.0.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OtherTextIsUtf16BeBeforePdf2()
    {
        var bytes = PdfText.Encode(Omega, Version17);
        await Assert.That(bytes.AsSpan().StartsWith(Utf16Mark)).IsTrue();
        await Assert.That(PdfText.Decode(bytes)).IsEqualTo(Omega);
        await Assert.That(PdfText.Encode(Omega)).IsEquivalentTo(bytes);
    }

    /// <summary>Text outside PDFDocEncoding is UTF-8 with a byte order mark in PDF 2.0.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OtherTextIsUtf8InPdf2()
    {
        var bytes = PdfText.Encode(Omega, Version20);
        await Assert.That(bytes.AsSpan().StartsWith(Utf8Mark)).IsTrue();
        await Assert.That(Encoding.UTF8.GetString(bytes, Utf8Mark.Length, bytes.Length - Utf8Mark.Length)).IsEqualTo(Omega);
        await Assert.That(PdfText.Decode(bytes)).IsEqualTo(Omega);
    }

    /// <summary>All three encodings, and UTF-16LE with a mark, decode.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadersDecodeEveryEncoding()
    {
        await Assert.That(PdfText.Decode(CafeUtf16)).IsEqualTo("Café");
        await Assert.That(PdfText.Decode(CafeUtf8)).IsEqualTo("Café");
        await Assert.That(PdfText.Decode([(byte)'C', (byte)'a', (byte)'f', EAcuteCode])).IsEqualTo("Café");
        await Assert.That(PdfText.Decode([0xFF, 0xFE, (byte)'H', 0, (byte)'i', 0])).IsEqualTo("Hi");
    }

    /// <summary>A replacement keeps the encoding of the string it replaces.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReplacementKeepsPreviousEncoding()
    {
        await Assert.That(PdfText.Encode("abc", CafeUtf16, Version20)[0]).IsEqualTo((byte)0xFE);
        await Assert.That(PdfText.Encode("abc", CafeUtf8, Version17)[0]).IsEqualTo((byte)0xEF);
        await Assert.That(PdfText.Encode("abc", "old"u8, Version20)).IsEquivalentTo("abc"u8.ToArray());
        await Assert.That(PdfText.Encode(Omega, "old"u8, Version17)[0]).IsEqualTo((byte)0xFE);
        await Assert.That(PdfText.Encode(Omega, "old"u8, Version20)[0]).IsEqualTo((byte)0xEF);
        await Assert.That(PdfText.Encode(Omega, ReadOnlySpan<byte>.Empty, Version20)[0]).IsEqualTo((byte)0xEF);
    }

    /// <summary>Strings nobody touched are written byte for byte by both writers, in 1.7 and 2.0 files.</summary>
    /// <param name="version">The header version.</param>
    /// <param name="compact">Whether to rewrite the file rather than append an update.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(Version17, false)]
    [Arguments(Version17, true)]
    [Arguments(Version20, false)]
    [Arguments(Version20, true)]
    public async Task UntouchedStringsSurviveSaving(string version, bool compact)
    {
        using var document = PdfDocumentReader.Open(Build(version, CafeUtf16Hex), null);
        var saved = Save(document, compact);
        using var reopened = PdfDocumentReader.Open(saved, null);
        await Assert.That(Bytes(Info(reopened), KnownName.Title)).IsEquivalentTo(CafeUtf16);
        await Assert.That(Bytes(Find(reopened, KnownName.Title, KnownName.Parent), KnownName.Title)).IsEquivalentTo(CafeUtf16);
        await Assert.That(Bytes(Find(reopened, KnownName.Contents, KnownName.Rect), KnownName.Contents)).IsEquivalentTo(CafeUtf16);
        await Assert.That(Bytes(Find(reopened, KnownName.V, KnownName.T), KnownName.V)).IsEquivalentTo(CafeUtf16);
    }

    /// <summary>UTF-8 strings of a 2.0 file also survive both writers unchanged.</summary>
    /// <param name="compact">Whether to rewrite the file rather than append an update.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Utf8StringsSurviveSaving(bool compact)
    {
        using var document = PdfDocumentReader.Open(Build(Version20, CafeUtf8Hex), null);
        using var reopened = PdfDocumentReader.Open(Save(document, compact), null);
        await Assert.That(Bytes(Info(reopened), KnownName.Title)).IsEquivalentTo(CafeUtf8);
        await Assert.That(Bytes(Find(reopened, KnownName.V, KnownName.T), KnownName.V)).IsEquivalentTo(CafeUtf8);
    }

    /// <summary>A metadata edit keeps UTF-16 in a 2.0 file, keeps UTF-8, and falls back to the version's choice for PDFDocEncoding that no longer fits.</summary>
    /// <param name="version">The header version.</param>
    /// <param name="previousHex">The hex of the title being replaced.</param>
    /// <param name="text">The new title.</param>
    /// <param name="first">The expected first byte of the stored title.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(Version17, CafeUtf16Hex, "abc", (byte)0xFE)]
    [Arguments(Version20, CafeUtf16Hex, "abc", (byte)0xFE)]
    [Arguments(Version20, CafeUtf8Hex, "abc", (byte)0xEF)]
    [Arguments(Version17, CafeUtf8Hex, "abc", (byte)0xEF)]
    [Arguments(Version17, "416263", Omega, (byte)0xFE)]
    [Arguments(Version20, "416263", Omega, (byte)0xEF)]
    [Arguments(Version17, "416263", "abc", (byte)'a')]
    public async Task MetadataEditKeepsEncoding(string version, string previousHex, string text, byte first)
    {
        using var document = PdfDocumentReader.Open(Build(version, previousHex), null);
        PdfDocumentMetadataEditing.SetMetadata(document, new() { Title = text });
        using var reopened = PdfDocumentReader.Open(PdfIncrementalWriter.Save(document.Objects), null);
        var stored = Bytes(Info(reopened), KnownName.Title);
        await Assert.That(stored[0]).IsEqualTo(first);
        await Assert.That(PdfDocumentMetadata.GetInfo(reopened).Title).IsEqualTo(text);
    }

    /// <summary>A form edit keeps the encoding of the value it replaces, and the value reads back after both saves.</summary>
    /// <param name="version">The header version.</param>
    /// <param name="previousHex">The hex of the value being replaced.</param>
    /// <param name="text">The new value.</param>
    /// <param name="first">The expected first byte of the stored value.</param>
    /// <param name="compact">Whether to rewrite the file rather than append an update.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(Version17, CafeUtf16Hex, "abc", (byte)0xFE, false)]
    [Arguments(Version20, CafeUtf16Hex, "abc", (byte)0xFE, true)]
    [Arguments(Version20, CafeUtf8Hex, "abc", (byte)0xEF, false)]
    [Arguments(Version17, "416263", Omega, (byte)0xFE, true)]
    [Arguments(Version20, "416263", Omega, (byte)0xEF, false)]
    public async Task FormEditKeepsEncoding(string version, string previousHex, string text, byte first, bool compact)
    {
        using var document = PdfDocumentReader.Open(Build(version, previousHex), null);
        await Assert.That(HyperPdfLibrary.Forms.PdfFormEditing.SetText(PdfDocumentForms.GetForm(document), 0, FieldIndex, text)).IsTrue();
        using var reopened = PdfDocumentReader.Open(Save(document, compact), null);
        var stored = Bytes(Find(reopened, KnownName.V, KnownName.T), KnownName.V);
        await Assert.That(stored[0]).IsEqualTo(first);
        await Assert.That(PdfText.Decode(stored)).IsEqualTo(text);
    }

    /// <summary>An annotation text edit keeps the encoding of the entry it replaces.</summary>
    /// <param name="version">The header version.</param>
    /// <param name="previousHex">The hex of the text being replaced.</param>
    /// <param name="text">The new text.</param>
    /// <param name="first">The expected first byte.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(Version20, CafeUtf16Hex, "abc", (byte)0xFE)]
    [Arguments(Version20, CafeUtf8Hex, "abc", (byte)0xEF)]
    [Arguments(Version17, "416263", Omega, (byte)0xFE)]
    [Arguments(Version20, "416263", Omega, (byte)0xEF)]
    [Arguments(Version17, "416263", "xyz", (byte)'x')]
    public async Task AnnotationEditKeepsEncoding(string version, string previousHex, string text, byte first)
    {
        using var document = PdfDocumentReader.Open(Build(version, previousHex), null);
        var annotation = Find(document, KnownName.Contents, KnownName.Rect).Clone();
        PdfAnnotations.SetText(annotation, KnownName.Contents, text);
        var stored = Bytes(annotation, KnownName.Contents);
        await Assert.That(stored[0]).IsEqualTo(first);
        await Assert.That(PdfText.Decode(stored)).IsEqualTo(text);
    }

    /// <summary>New text round trips through a save and reopen in 1.7 and 2.0 files, with the encoding the version calls for.</summary>
    /// <param name="version">The header version.</param>
    /// <param name="compact">Whether to rewrite the file rather than append an update.</param>
    /// <param name="first">The expected first byte of the stored title.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(Version17, false, (byte)0xFE)]
    [Arguments(Version17, true, (byte)0xFE)]
    [Arguments(Version20, false, (byte)0xEF)]
    [Arguments(Version20, true, (byte)0xEF)]
    public async Task NewTextRoundTrips(string version, bool compact, byte first)
    {
        using var document = PdfDocumentReader.Open(Build(version, "416263"), null);
        PdfDocumentMetadataEditing.SetMetadata(document, new() { Title = Omega });
        using var reopened = PdfDocumentReader.Open(Save(document, compact), null);
        await Assert.That(PdfDocumentMetadata.GetInfo(reopened).Title).IsEqualTo(Omega);
        await Assert.That(Bytes(Info(reopened), KnownName.Title)[0]).IsEqualTo(first);
    }

    /// <summary>Saves a document with the chosen writer.</summary>
    /// <param name="document">The document.</param>
    /// <param name="compact">Whether to rewrite the file.</param>
    /// <returns>The saved bytes.</returns>
    private static byte[] Save(PdfDocument document, bool compact) => compact ? PdfCompactWriter.Save(document.Objects, PdfCompactOptions.Default) : PdfIncrementalWriter.Save(document.Objects);

    /// <summary>Gets the bytes of a string entry.</summary>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="key">The key.</param>
    /// <returns>The bytes.</returns>
    private static byte[] Bytes(PdfDictionary dictionary, KnownName key) => dictionary.GetStringBytes(key).ToArray();

    /// <summary>Gets the information dictionary.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The dictionary.</returns>
    private static PdfDictionary Info(PdfDocument document) => StoreReading.Resolve(document.Objects, document.Objects.Trailer.GetRaw(KnownName.Info)).AsDictionary()!;

    /// <summary>Finds the first object dictionary that holds a string under one key and has another key.</summary>
    /// <param name="document">The document.</param>
    /// <param name="text">The string entry.</param>
    /// <param name="other">The other required key.</param>
    /// <returns>The dictionary.</returns>
    /// <exception cref="InvalidOperationException">No object matches.</exception>
    private static PdfDictionary Find(PdfDocument document, KnownName text, KnownName other)
    {
        for (var number = 1; number < document.Objects.Size; number++)
        {
            if (StoreReading.GetDictionary(document.Objects, new(number, 0)) is { } dictionary && dictionary.Get(text).Kind == PdfKind.String && !dictionary.Get(other).IsNull)
            {
                return dictionary;
            }
        }

        throw new InvalidOperationException("No matching object.");
    }

    /// <summary>Builds a one page document whose title, outline title, annotation text and field value all hold one string.</summary>
    /// <param name="version">The header version.</param>
    /// <param name="hex">The string, as hex digits.</param>
    /// <returns>The file.</returns>
    private static byte[] Build(string version, string hex)
    {
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R /Outlines 6 0 R /AcroForm << /Fields [5 0 R] /DA (/Helv 12 Tf 0 g) >> >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Annots [4 0 R 5 0 R] >>",
            $"<< /Type /Annot /Subtype /Text /Rect [10 10 30 30] /Contents <{hex}> >>",
            $"<< /Type /Annot /Subtype /Widget /FT /Tx /T (F) /Rect [10 50 150 70] /P 3 0 R /F 4 /DA (/Helv 12 Tf 0 g) /V <{hex}> >>",
            "<< /Type /Outlines /First 7 0 R /Last 7 0 R /Count 1 >>",
            $"<< /Title <{hex}> /Parent 6 0 R >>",
            $"<< /Title <{hex}> >>",
        };
        var output = new StringBuilder($"%PDF-{version}\n");
        var offsets = new int[objects.Length];
        for (var i = 0; i < objects.Length; i++)
        {
            offsets[i] = output.Length;
            _ = output.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }

        var xref = output.Length;
        _ = output.Append($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            _ = output.Append($"{offset:D10} 00000 n \n");
        }

        _ = output.Append($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R /Info 8 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.Latin1.GetBytes(output.ToString());
    }
}
