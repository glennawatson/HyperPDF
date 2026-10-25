// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Requirements;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures reading and checking document requirements from a warm document: a document with no <c>/Requirements</c>
/// entry, which must cost nothing, and one with six requirements, a version, a penalty and a script handler. Allocations
/// come from the EventPipe trace.
/// </summary>
public class HyperPdfRequirementBenchmarks
{
    /// <summary>A document with no requirements.</summary>
    private PdfDocument _plain = null!;

    /// <summary>A document with six requirements.</summary>
    private PdfDocument _requirements = null!;

    /// <summary>Opens both documents and reads each once so its objects are parsed.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _plain = PdfDocumentReader.Open(Build(string.Empty), null);
        _requirements = PdfDocumentReader.Open(
            Build(
                "/Requirements [<< /S /Navigation /V /2.0 /Penalty 0 >> << /S /Transitions /Penalty 20 >> << /S /Collection /V /1.7 /Penalty 5 >> "
                + "<< /S /Markup /V /2.1 /Penalty 15 >> << /S /ADBE_Vendor /Penalty 10 /RH [<< /S /JS /Script (Check) >> << /S /NoOp >>] >> << /S /Attachment >>]"),
            null);
        _ = ReadNone();
        _ = ReadSix();
        _ = EvaluateSix();
    }

    /// <summary>Closes the documents.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _plain.Dispose();
        _requirements.Dispose();
    }

    /// <summary>Reads the requirements of a document that has none.</summary>
    /// <returns>The requirement count.</returns>
    [Benchmark]
    public int ReadNone() => PdfDocumentRequirements.GetRequirements(_plain).Requirements.Count;

    /// <summary>Checks the requirements of a document that has none.</summary>
    /// <returns>The total penalty.</returns>
    [Benchmark]
    public int EvaluateNone() => PdfDocumentRequirements.Evaluate(_plain).TotalPenalty;

    /// <summary>Reads six requirement dictionaries.</summary>
    /// <returns>The requirement count.</returns>
    [Benchmark]
    public int ReadSix() => PdfDocumentRequirements.GetRequirements(_requirements).Requirements.Count;

    /// <summary>Reads and checks six requirements against the viewer's capabilities.</summary>
    /// <returns>The total penalty.</returns>
    [Benchmark]
    public int EvaluateSix() => PdfDocumentRequirements.Evaluate(_requirements).TotalPenalty;

    /// <summary>Builds a one page document.</summary>
    /// <param name="catalogEntries">Extra catalog entries.</param>
    /// <returns>The file bytes.</returns>
    private static byte[] Build(string catalogEntries) => MiniPdf.Build(
        $"<< /Type /Catalog /Pages 2 0 R /Version /2.0 {catalogEntries} >>",
        "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] >>");
}
