// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdf.Benchmarks;

/// <summary>Measures effective non-interactive field attributes and retained-snapshot access.</summary>
public class PrintFieldReadingBenchmarks
{
    /// <summary>The parsed source document.</summary>
    private PdfDocument _document = null!;

    /// <summary>The prepared field dictionary.</summary>
    private PdfDictionary _element = null!;

    /// <summary>The caller-owned reader reused between operations.</summary>
    private StructureAttributeReader _reader = null!;

    /// <summary>The already decoded attributes.</summary>
    private PdfStructureAttributes _attributes = null!;

    /// <summary>Gets or sets the attribute owner, including omission and an excluded owner.</summary>
    [Params("PrintField", "Namespace", "Omitted", "Table")]
    public string Owner { get; set; } = "PrintField";

    /// <summary>Parses the synthetic field outside measurement.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var owner = Owner == "Namespace" ? "/O /NSO /NS << /NS (PrintField) >>" : $"/O /{Owner}";
        var attributes = Owner == "Omitted" ? string.Empty : $"/A << {owner} /Role /cb /Checked /on /Desc (Accept terms) >>";
        _document = PdfDocumentReader.Open(
            MiniPdf.Build(
            [
                "<< /Type /Catalog /Pages 2 0 R /StructTreeRoot 3 0 R >>",
                "<< /Type /Pages /Count 0 /Kids [] >>",
                "<< /Type /StructTreeRoot /K 4 0 R >>",
                $"<< /Type /StructElem /S /Form /P 3 0 R {attributes} >>",
            ]),
            null);
        _element = PdfDocumentTagged.GetStructureTree(_document)!.Roots[0].Dictionary;
        var names = _document.Objects.Names;
        _reader = new(new TaggedNames(names), names, null);
        _attributes = _reader.Read(_element);
    }

    /// <summary>Disposes the source document after measurement.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document.Dispose();

    /// <summary>Decodes effective fields from an already parsed attribute dictionary.</summary>
    /// <returns>The retained reading snapshot.</returns>
    [Benchmark]
    public PdfStructureAttributes ReadAttributes() => _reader.Read(_element);

    /// <summary>Reads the checked state from an existing snapshot without reparsing or copying text.</summary>
    /// <returns>The source checked-state name, or null.</returns>
    [Benchmark]
    public string? ReadChecked() => _attributes.PrintField.Checked;
}
