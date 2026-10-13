// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;

namespace HyperPdf.Benchmarks;

/// <summary>Measures reading multimedia, 3D and rich media annotations, XFA packets and document scripts as data.</summary>
public class HyperPdfMediaDataReadBenchmarks
{
    /// <summary>The number of multimedia annotations in the fixture.</summary>
    private const int AnnotationCount = 5;

    /// <summary>The number of XFA packets in the fixture.</summary>
    private const int PacketCount = 3;

    /// <summary>The fixture.</summary>
    private PdfDocument? _document;

    /// <summary>Opens a document with a Screen, a Movie, a Sound, a RichMedia and a 3D annotation, three XFA packets and two scripts.</summary>
    /// <exception cref="InvalidOperationException">The fixture does not read as expected.</exception>
    [GlobalSetup]
    public void Setup()
    {
        var bytes = MiniPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R /Names << /JavaScript 8 0 R >> /AcroForm << /Fields [] /XFA [(template) 9 0 R (datasets) 10 0 R (config) 11 0 R] >> >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 300] /Annots [4 0 R 5 0 R 6 0 R 7 0 R 12 0 R] >>",
            "<< /Type /Annot /Subtype /Screen /Rect [0 0 50 50] /T (Screen) /A << /S /Rendition /OP 0 /R << /S /MR /N (Clip) /C << /S /MCD /D (clip.mp4) >> >> >> >>",
            "<< /Type /Annot /Subtype /Movie /Rect [50 0 100 50] /T (Movie) /Movie << /F (movie.mov) /Aspect [640 480] /Poster true >> /A << /Rate 2 /Mode /Repeat >> >>",
            "<< /Type /Annot /Subtype /Sound /Rect [100 0 150 50] /Sound 13 0 R >>",
            "<< /Type /Annot /Subtype /RichMedia /Rect [150 0 200 50] /RichMediaContent << /Assets << /Names [(a.swf) 14 0 R] >> "
            + "/Configurations [<< /Subtype /Flash /Instances [<< /Subtype /Flash /Asset 14 0 R >>] >>] >> >>",
            "<< /Names [(init) 15 0 R (other) 16 0 R] >>",
            MiniPdf.Stream(string.Empty, "<template/>"),
            MiniPdf.Stream(string.Empty, "<xfa:datasets xmlns:xfa=\"http://www.xfa.org/schema/xfa-data/1.0/\"/>"),
            MiniPdf.Stream(string.Empty, "<config/>"),
            "<< /Type /Annot /Subtype /3D /Rect [200 0 250 50] /3DD 17 0 R >>",
            MiniPdf.Stream("/Type /Sound /R 22050 /C 2 /B 16", "sound data"),
            "<< /Type /Filespec /F (a.swf) /EF << /F 18 0 R >> >>",
            "<< /S /JavaScript /JS (var a = 1;) >>",
            "<< /S /JavaScript /JS 19 0 R >>",
            MiniPdf.Stream("/Type /3D /Subtype /U3D /VA [<< /XN (View) /C2W [1 0 0 0 1 0 0 0 1 0 0 0] >>]", "3d data"),
            MiniPdf.Stream("/Type /EmbeddedFile", "swf data"),
            MiniPdf.Stream(string.Empty, "var b = 2;"));
        _document = PdfDocumentReader.Open(bytes, null);
        if (PdfDocumentMedia.GetMultimediaAnnotations(_document).Length != AnnotationCount
            || PdfDocumentXfa.GetXfa(_document)?.Packets.Count != PacketCount
            || PdfDocumentActions.GetDocumentScripts(_document).Length != 2)
        {
            throw new InvalidOperationException("The media benchmark fixture did not read as expected.");
        }
    }

    /// <summary>Closes the fixture.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document?.Dispose();

    /// <summary>Tests for XFA data without reading it.</summary>
    /// <returns>Whether the document has XFA data.</returns>
    [Benchmark(Baseline = true)]
    public bool HasXfa() => PdfDocumentXfa.HasXfa(_document!);

    /// <summary>Reads every multimedia annotation of the document.</summary>
    /// <returns>The number of annotations.</returns>
    [Benchmark]
    public int ReadMultimediaAnnotations() => PdfDocumentMedia.GetMultimediaAnnotations(_document!).Length;

    /// <summary>Reads and decodes the XFA packets.</summary>
    /// <returns>The number of packets.</returns>
    [Benchmark]
    public int ReadXfaPackets() => PdfDocumentXfa.GetXfa(_document!)!.Packets.Count;

    /// <summary>Reads and decodes the document scripts.</summary>
    /// <returns>The number of scripts.</returns>
    [Benchmark]
    public int ReadDocumentScripts() => PdfDocumentActions.GetDocumentScripts(_document!).Length;
}
