// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Forms;

namespace HyperPdf.Benchmarks;

/// <summary>Reads the calculation order of a form with 2,000 calculated fields.</summary>
public class HyperPdfCalculationOrderBenchmarks
{
    /// <summary>The object number of the first field.</summary>
    private const int FirstField = 4;

    /// <summary>The field names read, reused between runs.</summary>
    private readonly List<string> _names = [];

    /// <summary>The form document.</summary>
    private PdfDocument _document = null!;

    /// <summary>The form.</summary>
    private PdfForm _form = null!;

    /// <summary>Gets or sets the calculated fields in the form.</summary>
    [Params(200, 2000, 8000)]
    public int Fields { get; set; }

    /// <summary>Builds the form outside the measured operations.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _document = PdfDocumentReader.Open(Build(Fields), null);
        _form = PdfDocumentForms.GetForm(_document);
    }

    /// <summary>Closes the document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document.Dispose();

    /// <summary>Reads the names of the fields in calculation order.</summary>
    /// <returns>The fields listed.</returns>
    [Benchmark]
    public int ReadOrder()
    {
        _names.Clear();
        PdfFormOrder.GetCalculationOrder(_form, _names);
        return _names.Count;
    }

    /// <summary>Builds the form.</summary>
    /// <param name="count">The calculated fields.</param>
    /// <returns>The file.</returns>
    private static byte[] Build(int count)
    {
        var fields = new StringBuilder();
        for (var i = 0; i < count; i++)
        {
            _ = fields.Append(CultureInfo.InvariantCulture, $"{FirstField + i} 0 R ");
        }

        List<string> objects =
        [
            $"<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [{fields}] /CO [{fields}] /DA (/Helv 0 Tf 0 g) >> >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [{fields}] >>",
        ];
        for (var i = 0; i < count; i++)
        {
            objects.Add($"<< /Type /Annot /Subtype /Widget /FT /Tx /T (Field{i}) /Rect [0 0 10 10] /P 3 0 R >>");
        }

        return MiniPdf.Build([.. objects]);
    }
}
