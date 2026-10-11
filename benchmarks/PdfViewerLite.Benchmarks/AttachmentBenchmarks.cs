// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using PdfViewerLite.Core.Attachments;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.Benchmarks;

/// <summary>Measures listing a document's embedded files and copying one out; allocations come from the EventPipe trace.</summary>
public class AttachmentBenchmarks
{
    /// <summary>The destination stream, reused so only the library's allocations are measured.</summary>
    private readonly MemoryStream _destination = new();

    /// <summary>The document.</summary>
    private BenchmarkPdfDocument _document = null!;

    /// <summary>The document as an attachment source.</summary>
    private IAttachmentSource _source = null!;

    /// <summary>Opens a document with one embedded file.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _document = new(TestPdf.CreateWithAttachment());
        _source = PdfViewerLite.Core.Documents.DocumentFeature<IAttachmentSource>.Cast(_document.Document)!;
        _ = _source.GetAttachments();
    }

    /// <summary>Closes the document.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _document.Dispose();
        _destination.Dispose();
    }

    /// <summary>Lists the embedded files.</summary>
    /// <returns>The count.</returns>
    [Benchmark]
    public int ListAttachments() => _source.GetAttachments().Count;

    /// <summary>Copies the embedded file into a stream.</summary>
    /// <returns>Whether it was copied.</returns>
    [Benchmark]
    public bool SaveAttachment()
    {
        _destination.Position = 0;
        return _source.SaveAttachment(0, _destination);
    }
}
