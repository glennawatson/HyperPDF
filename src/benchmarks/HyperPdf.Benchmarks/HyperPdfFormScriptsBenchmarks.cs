// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Forms.Scripting;
using HyperPdf.Engine;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;

namespace HyperPdf.Benchmarks;

/// <summary>Reads the scripts of a page of 300 fields that repeat the same few format and keystroke scripts, as price and date columns of a form do.</summary>
public class HyperPdfFormScriptsBenchmarks
{
    /// <summary>The fields on the page.</summary>
    private const int Fields = 300;

    /// <summary>The spacing between fields.</summary>
    private const int Spacing = 2;

    /// <summary>The scripts read, reused between runs.</summary>
    private readonly List<FieldScripts> _scripts = [];

    /// <summary>The managed document.</summary>
    private PdfDocument _document = null!;

    /// <summary>The engine document.</summary>
    private HyperPdfDocument _engine = null!;

    /// <summary>Builds the form outside the measured operations.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _document = PdfDocumentReader.Open(BuildForm(), null);
        _engine = new(_document, "form-scripts-benchmark.pdf");
    }

    /// <summary>Closes the document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document.Dispose();

    /// <summary>Reads and recognises the scripts of every field on the page.</summary>
    /// <returns>The fields with scripts.</returns>
    [Benchmark]
    public int GetPageScripts()
    {
        _scripts.Clear();
        HyperPdfDocumentFormScripts.GetScripts(_engine, 0, _scripts);
        return _scripts.Count;
    }

    /// <summary>Builds a page of text fields that share a number format and keystroke script.</summary>
    /// <returns>The file.</returns>
    private static byte[] BuildForm()
    {
        const string format = "AFNumber_Format(2, 0, 0, 0, \\\"$\\\", true);";
        const string keystroke = "AFNumber_Keystroke(2, 0, 0, 0, \\\"$\\\", true);";
        var annots = new StringBuilder();
        List<string> objects = [string.Empty, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>", string.Empty, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"];
        for (var i = 0; i < Fields; i++)
        {
            _ = annots.Append(CultureInfo.InvariantCulture, $"{objects.Count + 1} 0 R ");
            objects.Add(
                $"<< /Type /Annot /Subtype /Widget /FT /Tx /T (F{i}) /Rect [0 {i * Spacing} 10 {(i * Spacing) + 1}] /P 3 0 R "
                + $"/AA << /F << /S /JavaScript /JS ({format}) >> /K << /S /JavaScript /JS ({keystroke}) >> >> >>");
        }

        objects[0] = $"<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [{annots}] /DA (/Helv 0 Tf 0 g) >> >>";
        objects[2] = $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [{annots}] >>";
        return MiniPdf.Build([.. objects]);
    }
}
