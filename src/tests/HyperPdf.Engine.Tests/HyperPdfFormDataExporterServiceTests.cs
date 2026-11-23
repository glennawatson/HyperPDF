// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Forms;
using HyperPdf.TestAssets;

namespace HyperPdf.Engine.Tests;

/// <summary>Tests for <see cref="HyperPdfFormDataExporterService"/>.</summary>
public sealed class HyperPdfFormDataExporterServiceTests
{
    /// <summary>The start of every FDF file.</summary>
    private const string FdfMark = "%FDF";

    /// <summary>A form's answers come out as an FDF file and as an XFDF file, and the importer reads its own FDF back.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Export_WritesFdfAndXfdfThatImportBack()
    {
        using var test = new HyperPdfTestDocument(TestPdf.CreateForm());
        var exporter = DocumentFeature<IFormDataExporter>.Cast(test.Document)!;
        var importer = DocumentFeature<IFormDataImporter>.Cast(test.Document)!;

        var fdf = exporter.Export(FormDataFormat.Fdf);
        var xfdf = Encoding.UTF8.GetString(exporter.Export(FormDataFormat.Xfdf));
        var imported = importer.Import(fdf);

        await Assert.That(Encoding.ASCII.GetString(fdf, 0, FdfMark.Length)).IsEqualTo(FdfMark);
        await Assert.That(xfdf).Contains("<xfdf");
        await Assert.That(xfdf).Contains("<field");
        await Assert.That(imported.Succeeded).IsTrue();
    }
}
