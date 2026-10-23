// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using System.Text.Json;
using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Json;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Json;

/// <summary>Checks that a document exports as qpdf-shaped JSON and that the JSON applies back.</summary>
public sealed class PdfJsonTests
{
    /// <summary>The title the test document has.</summary>
    private const string Title = "Original title";

    /// <summary>The version of the JSON layout.</summary>
    private const int JsonVersion = 2;

    /// <summary>The page size, in points.</summary>
    private const float PageSize = 200F;

    /// <summary>The binary bytes used to test a string that is not text.</summary>
    private static readonly byte[] Binary = [0x00, 0xFF, 0x10, 0x80, 0x7F];

    /// <summary>Every object appears by number, with strings as text and a stream's data as base64.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ExportsObjectsStringsAndStreams()
    {
        using var document = PdfDocumentReader.Open(CreateFile(), null);

        var json = await ExportAsync(document, PdfJsonStreamData.Decoded);

        using var parsed = JsonDocument.Parse(json);
        var table = parsed.RootElement.GetProperty("qpdf")[1];
        var text = table.GetRawText();
        using (Assert.Multiple())
        {
            await Assert.That(parsed.RootElement.GetProperty("version").GetInt32()).IsEqualTo(JsonVersion);
            await Assert.That(text).Contains($"\"u:{Title}\"");
            await Assert.That(text).Contains("\"/Type\"");
            await Assert.That(table.TryGetProperty("trailer", out _)).IsTrue();
            await Assert.That(text).Contains("\"stream\"");
        }
    }

    /// <summary>The data of a content stream exports decoded, without its filters.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DecodedStreamsHaveNoFilters()
    {
        using var document = PdfDocumentReader.Open(CreateFile(), null);

        var decoded = await ExportAsync(document, PdfJsonStreamData.Decoded);
        var stored = await ExportAsync(document, PdfJsonStreamData.Stored);
        var none = await ExportAsync(document, PdfJsonStreamData.None);

        using (Assert.Multiple())
        {
            await Assert.That(decoded).DoesNotContain("FlateDecode");
            await Assert.That(stored).Contains("FlateDecode");
            await Assert.That(none).DoesNotContain("\"data\"");
        }
    }

    /// <summary>A changed string in the JSON changes the document, and the saved file reads back with it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AppliedChangesSurviveASave()
    {
        using var document = PdfDocumentReader.Open(CreateFile(), null);
        var json = await ExportAsync(document, PdfJsonStreamData.Decoded);
        var edited = json.Replace($"u:{Title}", "u:Changed title", StringComparison.Ordinal);

        var changed = PdfJsonUpdater.Apply(document, Encoding.UTF8.GetBytes(edited), CancellationToken.None);
        await using var saved = new MemoryStream();
        await PdfDocumentSaving.SaveAsync(document, saved, CancellationToken.None);

        using var reopened = PdfDocumentReader.Open(saved.ToArray(), null);
        var info = reopened.Objects.Trailer.GetDictionary(KnownName.Info);
        using (Assert.Multiple())
        {
            await Assert.That(changed).IsGreaterThan(0);
            await Assert.That(info!.GetText(KnownName.Title)).IsEqualTo("Changed title");
            await Assert.That(reopened.PageCount).IsEqualTo(1);
        }
    }

    /// <summary>A binary string and an odd name come back with the same bytes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BinaryStringsAndNamesRoundTrip()
    {
        using var document = PdfDocumentReader.Open(CreateFile(), null);
        var store = document.Objects;
        var id = StoreEditing.Add(store, PdfValue.Null);
        var dictionary = new PdfDictionary(store);
        dictionary.Set(store.Names.Intern("Blob"), PdfValue.FromString(Binary));
        dictionary.Set(store.Names.Intern("Plain name"), PdfValue.FromInteger(1));
        StoreEditing.Replace(store, id, PdfValue.FromDictionary(dictionary));
        var json = await ExportAsync(document, PdfJsonStreamData.None);

        using var other = PdfDocumentReader.Open(CreateFile(), null);
        _ = PdfJsonUpdater.Apply(other, Encoding.UTF8.GetBytes(json), CancellationToken.None);
        var copy = StoreReading.GetObject(other.Objects, id).AsDictionary();

        using (Assert.Multiple())
        {
            await Assert.That(json).Contains("b:00FF10807F");
            await Assert.That(copy).IsNotNull();
            await Assert.That(copy!.GetStringBytes(other.Objects.Names.Intern("Blob")).ToArray()).IsEquivalentTo(Binary);
            await Assert.That(copy.GetInteger(other.Objects.Names.Intern("Plain name"))).IsEqualTo(1);
        }
    }

    /// <summary>JSON that is not a qpdf file is refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RefusesJsonThatIsNotAQpdfFile()
    {
        using var document = PdfDocumentReader.Open(CreateFile(), null);

        await Assert.That(() => PdfJsonUpdater.Apply(document, "{\"a\":1}"u8.ToArray(), CancellationToken.None)).Throws<InvalidDataException>();
        await Assert.That(() => PdfJsonUpdater.Apply(document, "not json"u8.ToArray(), CancellationToken.None)).Throws<InvalidDataException>();
    }

    /// <summary>Exports a document as text.</summary>
    /// <param name="document">The document.</param>
    /// <param name="data">What to put in for stream data.</param>
    /// <returns>The JSON.</returns>
    private static async Task<string> ExportAsync(PdfDocument document, PdfJsonStreamData data)
    {
        await using var stream = new MemoryStream();
        await PdfJsonExporter.ExportAsync(document, stream, new(data, false), CancellationToken.None);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>Creates a one page document with a title and some content.</summary>
    /// <returns>The file.</returns>
    private static byte[] CreateFile()
    {
        var creator = new PdfDocumentCreator(new() { Title = Title });
        var canvas = creator.BeginPage(PageSize, PageSize);
        try
        {
            canvas.FillRectangle(0F, 0F, 0F, 0F, 0F, PageSize, PageSize);
            _ = canvas.Finish();
        }
        finally
        {
            canvas.Dispose();
        }

        return creator.ToArray();
    }
}
