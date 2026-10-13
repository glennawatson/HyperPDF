// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Navigation;

namespace HyperPdfLibrary.Tests.Document;

/// <summary>Tests for viewer preferences, output intents, developer extensions, language and the open action (ISO 32000-2 §7.7.2, §7.12, §12.2, §14.11.5).</summary>
public sealed class DocumentPreferencesTests
{
    /// <summary>The number of copies in the full preference sample.</summary>
    private const int Copies = 4;

    /// <summary>The colour components of the sample ICC profile stream.</summary>
    private const int ProfileComponents = 4;

    /// <summary>The extension level in the sample.</summary>
    private const int ExtensionLevel = 3;

    /// <summary>The default for every page boundary preference.</summary>
    private const string DefaultBoundary = "CropBox";

    /// <summary>The number of intents that survive in the damaged sample.</summary>
    private const int IntentCount = 4;

    /// <summary>The number of extensions in the sample.</summary>
    private const int ExtensionCount = 3;

    /// <summary>The bytes of the sample ICC profile.</summary>
    private const string ProfileBytes = "ICCPROFILE-BYTES";

    /// <summary>The page ranges in the full preference sample, as start and end pairs.</summary>
    private static readonly int[] RangeEnds = [1, 2, 5, 6];

    /// <summary>Every key of the viewer preferences dictionary is read.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EveryViewerPreferenceKeyIsRead()
    {
        using var document = StructureDocuments.Open(
            "/PageLayout /TwoPageRight /PageMode /FullScreen /ViewerPreferences << /HideToolbar true /HideMenubar true /HideWindowUI true /FitWindow true /CenterWindow true "
            + "/DisplayDocTitle true /NonFullScreenPageMode /UseThumbs /Direction /R2L /ViewArea /MediaBox /ViewClip /BleedBox /PrintArea /TrimBox /PrintClip /ArtBox "
            + "/PrintScaling /None /Duplex /DuplexFlipLongEdge /PickTrayByPDFSize true /PrintPageRange [1 2 5 6] /NumCopies 4 /Enforce [/PrintScaling /Duplex] >>");

        var prefs = PdfDocumentViewerPreferences.GetViewerPreferences(document);

        await Assert.That(prefs.PageLayout).IsEqualTo("TwoPageRight");
        await Assert.That(prefs.PageMode).IsEqualTo("FullScreen");
        await Assert.That(prefs.HideToolbar).IsTrue();
        await Assert.That(prefs.HideMenubar).IsTrue();
        await Assert.That(prefs.HideWindowUI).IsTrue();
        await Assert.That(prefs.FitWindow).IsTrue();
        await Assert.That(prefs.CenterWindow).IsTrue();
        await Assert.That(prefs.DisplayDocTitle).IsTrue();
        await Assert.That(prefs.NonFullScreenPageMode).IsEqualTo("UseThumbs");
        await Assert.That(prefs.Direction).IsEqualTo("R2L");
        await Assert.That(prefs.ViewArea).IsEqualTo("MediaBox");
        await Assert.That(prefs.ViewClip).IsEqualTo("BleedBox");
        await Assert.That(prefs.PrintArea).IsEqualTo("TrimBox");
        await Assert.That(prefs.PrintClip).IsEqualTo("ArtBox");
        await Assert.That(prefs.PrintScaling).IsEqualTo("None");
        await Assert.That(prefs.Duplex).IsEqualTo("DuplexFlipLongEdge");
        await Assert.That(prefs.PickTrayByPdfSize).IsTrue();
        await Assert.That(prefs.PrintPageRange).IsEquivalentTo(RangeEnds);
        await Assert.That(prefs.NumCopies).IsEqualTo(Copies);
        await Assert.That(prefs.Enforce).IsEquivalentTo(["PrintScaling", "Duplex"]);
    }

    /// <summary>A missing dictionary, an empty dictionary and a damaged dictionary all give the ISO 32000-2 Table 147 defaults.</summary>
    /// <param name="entries">The catalog entries.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("")]
    [Arguments("/ViewerPreferences << >>")]
    [Arguments("/ViewerPreferences 42")]
    [Arguments("/ViewerPreferences << /HideToolbar 7 /Direction 5 /NumCopies (x) /PrintPageRange (x) /Enforce 3 >>")]
    public async Task ViewerPreferencesUseDefaultsWhenAbsentOrDamaged(string entries)
    {
        using var document = StructureDocuments.Open(entries);

        var prefs = PdfDocumentViewerPreferences.GetViewerPreferences(document);

        await Assert.That(prefs.PageLayout).IsEqualTo("SinglePage");
        await Assert.That(prefs.PageMode).IsEqualTo("UseNone");
        await Assert.That(prefs.HideToolbar).IsFalse();
        await Assert.That(prefs.HideMenubar).IsFalse();
        await Assert.That(prefs.HideWindowUI).IsFalse();
        await Assert.That(prefs.FitWindow).IsFalse();
        await Assert.That(prefs.CenterWindow).IsFalse();
        await Assert.That(prefs.DisplayDocTitle).IsFalse();
        await Assert.That(prefs.NonFullScreenPageMode).IsEqualTo("UseNone");
        await Assert.That(prefs.Direction).IsEqualTo("L2R");
        await Assert.That(prefs.ViewArea).IsEqualTo(DefaultBoundary);
        await Assert.That(prefs.ViewClip).IsEqualTo(DefaultBoundary);
        await Assert.That(prefs.PrintArea).IsEqualTo(DefaultBoundary);
        await Assert.That(prefs.PrintClip).IsEqualTo(DefaultBoundary);
        await Assert.That(prefs.PrintScaling).IsEqualTo("AppDefault");
        await Assert.That(prefs.Duplex).IsNull();
        await Assert.That(prefs.PickTrayByPdfSize).IsFalse();
        await Assert.That(prefs.PrintPageRange).IsEmpty();
        await Assert.That(prefs.NumCopies).IsEqualTo(1);
        await Assert.That(prefs.Enforce).IsEmpty();
    }

    /// <summary>An output intent with an embedded ICC profile gives its fields and the profile bytes; damaged entries are skipped.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OutputIntentWithProfileIsRead()
    {
        using var document = StructureDocuments.Open(
            "/OutputIntents [4 0 R 42 (text) 6 0 R 7 0 R 8 0 R]",
            "<< /Type /OutputIntent /S /GTS_PDFX /OutputCondition (Coated FOGRA39) /OutputConditionIdentifier (FOGRA39) "
            + "/RegistryName (http://www.color.org) /Info (Info text) /DestOutputProfile 5 0 R >>",
            MiniPdf.Stream("/N 4", ProfileBytes),
            "<< /Type /OutputIntent >>",
            "<< /S /GTS_PDFA1 /OutputConditionIdentifier (sRGB) /DestOutputProfile 3 0 R >>",
            "<< /S /GTS_PDFA1 /OutputConditionIdentifier (none) >>");

        var intents = PdfDocumentCatalog.GetOutputIntents(document);

        await Assert.That(intents.Length).IsEqualTo(IntentCount);
        await Assert.That(intents[0].Subtype).IsEqualTo("GTS_PDFX");
        await Assert.That(intents[0].OutputCondition).IsEqualTo("Coated FOGRA39");
        await Assert.That(intents[0].OutputConditionIdentifier).IsEqualTo("FOGRA39");
        await Assert.That(intents[0].RegistryName).IsEqualTo("http://www.color.org");
        await Assert.That(intents[0].Info).IsEqualTo("Info text");
        await Assert.That(intents[0].ComponentCount).IsEqualTo(ProfileComponents);
        await Assert.That(System.Text.Encoding.ASCII.GetString(intents[0].Profile!.DecodeToArray())).IsEqualTo(ProfileBytes);
        await Assert.That(intents[1].Subtype).IsEmpty();
        await Assert.That(intents[1].Profile).IsNull();
        await Assert.That(intents[2].Profile).IsNull();
    }

    /// <summary>A page's output intents (PDF 2.0) are read apart from the catalog's.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PageOutputIntentsAreRead()
    {
        using var document = StructureDocuments.OpenPage(
            "/OutputIntents [5 0 R]",
            "/OutputIntents [4 0 R]",
            "<< /S /GTS_PDFX /OutputConditionIdentifier (page) >>",
            "<< /S /GTS_PDFA1 /OutputConditionIdentifier (catalog) >>");

        var page = PdfDocumentPages.GetPage(document, 0);

        await Assert.That(PdfDocumentCatalog.GetOutputIntents(document, page)[0].OutputConditionIdentifier).IsEqualTo("page");
        await Assert.That(PdfDocumentCatalog.GetOutputIntents(document)[0].OutputConditionIdentifier).IsEqualTo("catalog");
    }

    /// <summary>Developer extensions are read in dictionary and array form; damaged entries are skipped.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DeveloperExtensionsAreRead()
    {
        using var document = StructureDocuments.Open(
            "/Extensions << /ADBE << /BaseVersion /1.7 /ExtensionLevel 3 /URL (https://example.test/adbe) >> "
            + "/ISO_ [<< /BaseVersion /1.7 /ExtensionLevel 1 >> 5 << /BaseVersion /2.0 /ExtensionLevel 2 >>] /BAD 7 >>");

        var extensions = PdfDocumentExtensionDeclarations.GetDeveloperExtensions(document);

        await Assert.That(extensions.Length).IsEqualTo(ExtensionCount);
        await Assert.That(extensions[0].Prefix).IsEqualTo("ADBE");
        await Assert.That(extensions[0].BaseVersion).IsEqualTo("1.7");
        await Assert.That(extensions[0].ExtensionLevel).IsEqualTo(ExtensionLevel);
        await Assert.That(extensions[0].Url).IsEqualTo("https://example.test/adbe");
        await Assert.That(extensions[1].Prefix).IsEqualTo("ISO_");
        await Assert.That(extensions[2].BaseVersion).IsEqualTo("2.0");
        await Assert.That(PdfDocumentExtensionDeclarations.GetDeveloperExtensions(StructureDocuments.Open(string.Empty))).IsEmpty();
    }

    /// <summary>The catalog language is read, and a blank or missing one gives null.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LanguageIsRead()
    {
        using var document = StructureDocuments.Open("/Lang (en-AU)");
        using var blank = StructureDocuments.Open("/Lang ( )");
        using var none = StructureDocuments.Open(string.Empty);

        await Assert.That(PdfDocumentCatalog.GetLanguage(document)).IsEqualTo("en-AU");
        await Assert.That(PdfDocumentCatalog.GetLanguage(blank)).IsNull();
        await Assert.That(PdfDocumentCatalog.GetLanguage(none)).IsNull();
    }

    /// <summary>The open action is read as a destination or as an action dictionary, and is absent when the catalog has none.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OpenActionIsReadAsDestinationOrAction()
    {
        using var destination = StructureDocuments.Open("/OpenAction [3 0 R /Fit]");
        using var action = StructureDocuments.Open("/OpenAction << /S /URI /URI (https://example.test/) >>");
        using var none = StructureDocuments.Open(string.Empty);

        await Assert.That(PdfDocumentActions.GetOpenAction(destination)!.Action.Value is GoToAction).IsTrue();
        await Assert.That(PdfDocumentActions.GetOpenAction(action)!.Subtype).IsEqualTo("URI");
        await Assert.That(PdfDocumentActions.GetOpenAction(none)).IsNull();
    }
}
