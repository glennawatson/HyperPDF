// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Http.Remote;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;

namespace HyperPdf.Core.Tests.Remote;

/// <summary>Checks the stream that reads a document from a web server with range requests.</summary>
public sealed class HttpRangeStreamTests
{
    /// <summary>The size of the document the server holds.</summary>
    private const int DocumentLength = 100_000;

    /// <summary>An offset well into the document.</summary>
    private const int Middle = 40_000;

    /// <summary>How many bytes a test reads.</summary>
    private const int Chunk = 50;

    /// <summary>The pages of the large test PDF.</summary>
    private const int ManyPages = 3000;

    /// <summary>The lines of text on each page of the large test PDF.</summary>
    private const int LinesPerPage = 40;

    /// <summary>The share of the file, as a divisor, that opening and reading one page must stay under.</summary>
    private const int WholeFileShare = 10;

    /// <summary>The step between neighbouring bytes of the test document, so a misplaced read shows.</summary>
    private const int Stride = 7;

    /// <summary>How many bits an offset is shifted by to vary the bytes over longer stretches.</summary>
    private const int Shift = 8;

    /// <summary>Verifies that a server with range support gives a stream that knows the document's length.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Open_KnowsTheLength()
    {
        await using var server = RangeServer.Start(MakeDocument(), true);
        await using var stream = await HttpRangeStream.TryOpenAsync(server.Address, CancellationToken.None);

        await Assert.That(stream).IsNotNull();
        await Assert.That(stream!.Length).IsEqualTo(DocumentLength);
        await Assert.That(stream.CanSeek).IsTrue();
        await Assert.That(stream.CanWrite).IsFalse();
    }

    /// <summary>Verifies that a server that ignores range requests gives no stream, so the document is downloaded whole instead.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Open_IsNullWhenTheServerIgnoresRanges()
    {
        await using var server = RangeServer.Start(MakeDocument(), false);

        await Assert.That(await HttpRangeStream.TryOpenAsync(server.Address, CancellationToken.None)).IsNull();
    }

    /// <summary>Verifies that reading part of the document asks the server for that part only.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ReadAsync_ReadsOnlyTheRequestedPart()
    {
        var document = MakeDocument();
        await using var server = RangeServer.Start(document, true);
        await using var stream = await HttpRangeStream.TryOpenAsync(server.Address, CancellationToken.None);
        var before = server.Requests;
        var buffer = new byte[Chunk];

        stream!.Position = Middle;
        var read = await stream.ReadAsync(buffer, CancellationToken.None);

        await Assert.That(read).IsEqualTo(Chunk);
        await Assert.That(buffer.AsSpan().SequenceEqual(document.AsSpan(Middle, Chunk))).IsTrue();
        await Assert.That(server.Requests - before).IsEqualTo(1);
        await Assert.That(stream.Position).IsEqualTo(Middle + Chunk);
    }

    /// <summary>Verifies that the synchronous read gives the same bytes as the asynchronous one.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Read_ReadsTheSameBytes()
    {
        var document = MakeDocument();
        await using var server = RangeServer.Start(document, true);
        await using var stream = await HttpRangeStream.TryOpenAsync(server.Address, CancellationToken.None);
        var buffer = new byte[Chunk];

        _ = stream!.Seek(-Chunk, SeekOrigin.End);
        var read = stream.Read(buffer, 0, buffer.Length);

        await Assert.That(read).IsEqualTo(Chunk);
        await Assert.That(buffer.AsSpan().SequenceEqual(document.AsSpan(DocumentLength - Chunk))).IsTrue();
    }

    /// <summary>Verifies that reading at the end of the document gives nothing and asks for nothing.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ReadAsync_AtTheEndGivesNothing()
    {
        await using var server = RangeServer.Start(MakeDocument(), true);
        await using var stream = await HttpRangeStream.TryOpenAsync(server.Address, CancellationToken.None);
        var before = server.Requests;
        stream!.Position = DocumentLength;

        var read = await stream.ReadAsync(new byte[Chunk], CancellationToken.None);

        await Assert.That(read).IsEqualTo(0);
        await Assert.That(server.Requests).IsEqualTo(before);
    }

    /// <summary>Verifies that a large PDF opened over the stream shows its page count and first page after fetching a small part of the file.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task OpeningAPdf_FetchesOnlyAPartOfTheFile()
    {
        var pdf = TestPdf.CreateTextFilled(ManyPages, LinesPerPage);
        await using var server = RangeServer.Start(pdf, true);
        await using var stream = await HttpRangeStream.TryOpenAsync(server.Address, CancellationToken.None);

        using var document = await PdfDocumentReader.OpenAsync(stream!, null, CancellationToken.None);
        var page = await PdfDocumentText.GetTextPageAsync(document, 0, CancellationToken.None);

        await Assert.That(document.PageCount).IsEqualTo(ManyPages);
        await Assert.That(page.Text).Contains(TestPdf.Sentence);
        await Assert.That(server.BytesSent).IsLessThan(pdf.Length / WholeFileShare);
    }

    /// <summary>Verifies that an address that is not a web address is refused.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Open_RefusesAnAddressThatIsNotHttp() =>
        await Assert.That(static async () => await HttpRangeStream.TryOpenAsync(new("file:///tmp/x.pdf"), CancellationToken.None)).Throws<ArgumentException>();

    /// <summary>Makes a document whose byte at each offset can be told from its neighbours.</summary>
    /// <returns>The document.</returns>
    private static byte[] MakeDocument()
    {
        var content = new byte[DocumentLength];
        for (var index = 0; index < content.Length; index++)
        {
            content[index] = (byte)((index * Stride) + (index >> Shift));
        }

        return content;
    }
}
