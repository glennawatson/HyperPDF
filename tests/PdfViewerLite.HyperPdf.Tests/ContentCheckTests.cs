// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Documents;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.HyperPdf.Tests;

/// <summary>Tests finding content the viewer cannot show or run.</summary>
public sealed class ContentCheckTests
{
    /// <summary>A page index past the end of every sample.</summary>
    private const int MissingPage = 5;

    /// <summary>The sample documents.</summary>
    public enum Sample
    {
        /// <summary>A plain one page document.</summary>
        Plain = 0,

        /// <summary>A form with a text field, check box and combo box.</summary>
        Form = 1,

        /// <summary>A form whose scripts are all recognised.</summary>
        CalculatedForm = 2,

        /// <summary>A document using everything that cannot be shown.</summary>
        Unsupported = 3,

        /// <summary>A form with comb fields.</summary>
        CombForm = 4,

        /// <summary>A flat form.</summary>
        FlatForm = 5,

        /// <summary>A page with typewriter text.</summary>
        FreeText = 6,

        /// <summary>A tagged document.</summary>
        Tagged = 7,

        /// <summary>A document with layers.</summary>
        Layers = 8,

        /// <summary>A document with an attached file.</summary>
        Attachment = 9,
    }

    /// <summary>Verifies XFA forms, scripts, media players and 3D models are found, and a plain document has none.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FindsUnsupportedContent()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hyperpdf-unsupported-{Guid.NewGuid():N}.pdf");
        var plain = TestPdf.WriteTempFile(1);
        await File.WriteAllBytesAsync(path, TestPdf.CreateWithUnsupportedContent());
        try
        {
            using var document = new HyperPdfEngine().Open(path, null);
            var check = DocumentFeature<IContentCheck>.Cast(document)!;
            await Assert.That(check.CheckDocument()).IsEqualTo(UnsupportedContent.XfaForm | UnsupportedContent.JavaScript);
            await Assert.That(check.CheckPage(0)).IsEqualTo(UnsupportedContent.Multimedia | UnsupportedContent.ThreeD | UnsupportedContent.JavaScript);
            await Assert.That(check.CheckPage(1)).IsEqualTo(UnsupportedContent.None);

            using var simple = new HyperPdfEngine().Open(plain, null);
            await Assert.That(DocumentFeature<IContentCheck>.Cast(simple)!.CheckDocument()).IsEqualTo(UnsupportedContent.None);
            await Assert.That(DocumentFeature<IContentCheck>.Cast(simple)!.CheckPage(0)).IsEqualTo(UnsupportedContent.None);
        }
        finally
        {
            File.Delete(path);
            File.Delete(plain);
        }
    }

    /// <summary>A form whose scripts are all ones that are run is not flagged for JavaScript.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RecognisedFormScriptsAreNotFlagged()
    {
        using var test = new HyperPdfTestDocument(TestPdf.CreateCalculatedForm());
        var check = DocumentFeature<IContentCheck>.Cast(test.Document)!;
        await Assert.That(check.CheckPage(0)).IsEqualTo(UnsupportedContent.None);
        await Assert.That(check.CheckDocument()).IsEqualTo(UnsupportedContent.None);
    }

    /// <summary>Pages outside each sample document have no unsupported content.</summary>
    /// <param name="sample">The sample.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(Sample.Plain)]
    [Arguments(Sample.Form)]
    [Arguments(Sample.CalculatedForm)]
    [Arguments(Sample.Unsupported)]
    [Arguments(Sample.CombForm)]
    [Arguments(Sample.FlatForm)]
    [Arguments(Sample.FreeText)]
    [Arguments(Sample.Tagged)]
    [Arguments(Sample.Layers)]
    [Arguments(Sample.Attachment)]
    public async Task MissingPagesHaveNoUnsupportedContent(Sample sample)
    {
        using var test = new HyperPdfTestDocument(CreateSample(sample));
        var check = DocumentFeature<IContentCheck>.Cast(test.Document)!;
        await Assert.That(check.CheckPage(MissingPage)).IsEqualTo(UnsupportedContent.None);
        await Assert.That(check.CheckPage(-1)).IsEqualTo(UnsupportedContent.None);
    }

    /// <summary>Creates one of the sample documents.</summary>
    /// <param name="sample">The sample.</param>
    /// <returns>The PDF bytes.</returns>
    private static byte[] CreateSample(Sample sample) => sample switch
    {
        Sample.Plain => TestPdf.Create(1),
        Sample.Form => TestPdf.CreateForm(),
        Sample.CalculatedForm => TestPdf.CreateCalculatedForm(),
        Sample.Unsupported => TestPdf.CreateWithUnsupportedContent(),
        Sample.CombForm => TestPdf.CreateCombForm(),
        Sample.FlatForm => TestPdf.CreateFlatForm(),
        Sample.FreeText => TestPdf.CreateWithFreeText(),
        Sample.Tagged => TestPdf.CreateTagged(),
        Sample.Layers => TestPdf.CreateWithLayers(),
        _ => TestPdf.CreateWithAttachment(),
    };
}
