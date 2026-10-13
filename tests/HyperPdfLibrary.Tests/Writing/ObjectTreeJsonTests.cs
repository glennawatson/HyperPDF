// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using System.Text.Json;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Writing;

/// <summary>Tests for <see cref="PdfObjectTreeJson"/>.</summary>
public sealed class ObjectTreeJsonTests
{
    /// <summary>The number of objects in the unreferenced sample.</summary>
    private const int ObjectCount = 6;

    /// <summary>The most decoded bytes written in the capped test.</summary>
    private const int Cap = 4;

    /// <summary>The position of the unreferenced dictionary among the objects.</summary>
    private const int OrphanIndex = 4;

    /// <summary>The position of the unreferenced stream among the objects.</summary>
    private const int OrphanStreamIndex = 5;

    /// <summary>The position of the filtered content stream among the objects of the filtered sample.</summary>
    private const int ContentIndex = 3;

    /// <summary>The property that lists the objects.</summary>
    private const string ObjectsProperty = "objects";

    /// <summary>The property that holds an object's value.</summary>
    private const string ValueProperty = "value";

    /// <summary>The property of a reference.</summary>
    private const string RefProperty = "ref";

    /// <summary>The sample lists every object with its value, strings as text and the stream with its length and no data.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task ObjectsAreListedWithTheirValues(CancellationToken cancellationToken)
    {
        using var json = await WriteAsync(WriterControlSamples.WithOrphans(), new(), cancellationToken);

        var objects = json.RootElement.GetProperty(ObjectsProperty);
        await Assert.That(objects.GetArrayLength()).IsEqualTo(ObjectCount);
        await Assert.That(json.RootElement.GetProperty("version").GetString()).IsEqualTo("1.7");
        await Assert.That(json.RootElement.GetProperty("trailer").GetProperty(nameof(KnownName.Root)).GetProperty(RefProperty).GetString()).IsEqualTo("1 0");
        await Assert.That(objects[0].GetProperty(ValueProperty).GetProperty(nameof(KnownName.Type)).GetString()).IsEqualTo("/Catalog");
        await Assert.That(objects[OrphanIndex].GetProperty(ValueProperty).GetProperty("Orphan").GetProperty("string").GetString()).IsEqualTo(WriterControlSamples.OrphanMarker);
        var stream = objects[OrphanStreamIndex].GetProperty(ValueProperty).GetProperty("stream");
        await Assert.That(stream.GetProperty("rawLength").GetInt32()).IsEqualTo("orphan stream data".Length);
        await Assert.That(stream.TryGetProperty("decoded", out _)).IsFalse();
    }

    /// <summary>Decoded bytes are written as Base64 up to the cap, with the full decoded length, and a filter is named.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task DecodedBytesAreCapped(CancellationToken cancellationToken)
    {
        using var json = await WriteAsync(WriterControlSamples.FilteredStreams(false), new() { MaxDecodedBytes = Cap }, cancellationToken);

        var stream = json.RootElement.GetProperty(ObjectsProperty)[ContentIndex].GetProperty(ValueProperty).GetProperty("stream");
        await Assert.That(stream.GetProperty("filters")[0].GetString()).IsEqualTo("ASCIIHexDecode");
        await Assert.That(stream.GetProperty("decodedLength").GetInt32()).IsGreaterThan(Cap);
        await Assert.That(Encoding.ASCII.GetString(Convert.FromBase64String(stream.GetProperty("decoded").GetString()!))).IsEqualTo("1 0 ");
    }

    /// <summary>The async form writes the same JSON as the sync form.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task AsyncFormMatchesSyncForm(CancellationToken cancellationToken)
    {
        using var document = PdfDocumentReader.Open(WriterControlSamples.ContentPage(), null);
        var options = new PdfObjectTreeJsonOptions { Indented = false, MaxDecodedBytes = Cap };
        var sync = WriteSync(document, options);
        await using var async = new MemoryStream();

        await PdfObjectTreeJson.WriteAsync(document, async, options, cancellationToken);

        await Assert.That(async.ToArray().AsSpan().SequenceEqual(sync)).IsTrue();
    }

    /// <summary>A reference to the object itself is written as a reference, so a loop ends.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReferenceLoopsAreWrittenOnce(CancellationToken cancellationToken)
    {
        var loop = MiniPdf.Build("<< /Type /Catalog /Pages 2 0 R /Self 1 0 R >>", "<< /Type /Pages /Kids [] /Count 0 /Parent 2 0 R >>");

        using var json = await WriteAsync(loop, new(), cancellationToken);

        await Assert.That(json.RootElement.GetProperty(ObjectsProperty)[0].GetProperty(ValueProperty).GetProperty("Self").GetProperty(RefProperty).GetString()).IsEqualTo("1 0");
    }

    /// <summary>A cancelled token stops the write.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancelledWriteThrows()
    {
        using var document = PdfDocumentReader.Open(WriterControlSamples.WithOrphans(), null);
        using var source = new CancellationTokenSource();
        await source.CancelAsync();
        await using var output = new MemoryStream();

        _ = await Assert.That(async () => await PdfObjectTreeJson.WriteAsync(document, output, new(), source.Token)).Throws<OperationCanceledException>();
    }

    /// <summary>Writes a document's JSON view and parses it.</summary>
    /// <param name="file">The PDF file.</param>
    /// <param name="options">What to write.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The parsed JSON.</returns>
    private static async Task<JsonDocument> WriteAsync(byte[] file, PdfObjectTreeJsonOptions options, CancellationToken cancellationToken)
    {
        using var document = PdfDocumentReader.Open(file, null);
        await using var output = new MemoryStream();
        await PdfObjectTreeJson.WriteAsync(document, output, options, cancellationToken);
        return JsonDocument.Parse(output.ToArray());
    }

    /// <summary>Writes a document's JSON view with the sync form.</summary>
    /// <param name="document">The document.</param>
    /// <param name="options">What to write.</param>
    /// <returns>The JSON bytes.</returns>
    private static byte[] WriteSync(PdfDocument document, PdfObjectTreeJsonOptions options)
    {
        using var output = new MemoryStream();
        PdfObjectTreeJson.Write(document, output, options, CancellationToken.None);
        return output.ToArray();
    }
}
