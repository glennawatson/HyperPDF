// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Engine;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Interchange;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures importing an XFDF file into a form through the engine, which applies it as one undoable edit, against importing
/// the same file straight into the library document. Each operation opens the document, so <see cref="OpenOnly"/> is the
/// cost to take off the others. Allocations are checked from the EventPipe trace.
/// </summary>
public class HyperPdfFormDataImportBenchmarks
{
    /// <summary>An XFDF file with two fields the form has and one it has not.</summary>
    private static readonly byte[] Answers = """
        <?xml version="1.0" encoding="UTF-8"?>
        <xfdf xmlns="http://ns.adobe.com/xfdf/"><fields>
          <field name="Name"><value>new</value></field>
          <field name="Note"><value>a note</value></field>
          <field name="Missing"><value>x</value></field>
        </fields></xfdf>
        """u8.ToArray();

    /// <summary>The engine that opens the documents.</summary>
    private readonly HyperPdfEngine _engine = new();

    /// <summary>The file the form is opened from.</summary>
    private string _path = string.Empty;

    /// <summary>Writes the form to a file.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _path = Path.Combine(Path.GetTempPath(), $"hyperpdf-import-bench-{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(_path, FormImportSamples.Create());
    }

    /// <summary>Deletes the form file.</summary>
    [GlobalCleanup]
    public void Cleanup() => File.Delete(_path);

    /// <summary>Opens the document and nothing else: the cost the import benchmarks include.</summary>
    /// <returns>The page count.</returns>
    [Benchmark(Baseline = true)]
    public int OpenOnly()
    {
        using var document = (HyperPdfDocument)_engine.Open(_path, null);
        return document.PageCount;
    }

    /// <summary>Opens the document and imports the file straight into the library document, with no undo step.</summary>
    /// <returns>The fields applied.</returns>
    [Benchmark]
    public int ImportWithoutUndo()
    {
        using var document = (HyperPdfDocument)_engine.Open(_path, null);
        return PdfInterchange.ImportXfdf(document.Document, Answers).FieldsApplied;
    }

    /// <summary>Opens the document and imports the file through the engine's importer, as one undoable edit.</summary>
    /// <returns>The fields applied.</returns>
    [Benchmark]
    public int ImportAsUndoableEdit()
    {
        using var document = (HyperPdfDocument)_engine.Open(_path, null);
        return new HyperPdfFormDataImporter(document).Import(Answers).FieldsApplied;
    }
}
