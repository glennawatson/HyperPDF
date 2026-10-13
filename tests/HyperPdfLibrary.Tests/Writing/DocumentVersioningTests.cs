// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Writing;

/// <summary>Tests for reading and raising the version and for declaring developer extensions.</summary>
public sealed class DocumentVersioningTests
{
    /// <summary>The prefix of the test extension.</summary>
    private const string Prefix = "TEST";

    /// <summary>The level of the first declaration.</summary>
    private const int FirstLevel = 5;

    /// <summary>A level below the first.</summary>
    private const int LowerLevel = 3;

    /// <summary>A level above the first.</summary>
    private const int HigherLevel = 9;

    /// <summary>The number of declarations an array holds after two different levels.</summary>
    private const int TwoLevels = 2;

    /// <summary>The URL of the test extension.</summary>
    private const string Url = "https://example.com/extension";

    /// <summary>The text of the header of the sample file.</summary>
    private const string SampleHeader = "%PDF-1.7";

    /// <summary>The header put in its place, which is older than the features the tests add.</summary>
    private const string OldHeader = "%PDF-1.4";

    /// <summary>Parsing and comparing versions follows the numbers, not the text.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task VersionsParseAndCompare()
    {
        await Assert.That(PdfVersion.TryParse("1.7", out var seven)).IsTrue();
        await Assert.That(seven).IsEqualTo(PdfVersion.Pdf17);
        await Assert.That(PdfVersion.TryParse("1.10", out _)).IsFalse();
        await Assert.That(PdfVersion.TryParse("x.7", out _)).IsFalse();
        await Assert.That(PdfVersion.Pdf20 > PdfVersion.Pdf17).IsTrue();
        await Assert.That(PdfVersion.Pdf14 < PdfVersion.Pdf15).IsTrue();
        await Assert.That(PdfVersion.Pdf20.ToString()).IsEqualTo("2.0");
        await Assert.That(default(PdfVersion).IsUnset).IsTrue();
    }

    /// <summary>Raising the version sets the catalog's /Version, never lowers it, and an incremental save carries it while the header stays.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RaisingTheVersionSetsTheCatalogAndNeverLowers()
    {
        var source = OldHeaderDocument();
        using var document = PdfDocumentReader.Open(source, null);

        var raised = PdfDocumentVersioning.RaiseVersion(document, PdfVersion.Pdf17);
        var again = PdfDocumentVersioning.RaiseVersion(document, PdfVersion.Pdf17);
        var lower = PdfDocumentVersioning.RaiseVersion(document, PdfVersion.Pdf15);
        await using var output = new MemoryStream();
        await PdfDocumentSaving.SaveAsync(document, output, CancellationToken.None);
        using var reopened = PdfDocumentReader.Open(output.ToArray(), null);

        await Assert.That(raised).IsTrue();
        await Assert.That(again).IsFalse();
        await Assert.That(lower).IsFalse();
        await Assert.That(PdfDocumentVersioning.GetHeaderVersion(reopened)).IsEqualTo(PdfVersion.Pdf14);
        await Assert.That(PdfDocumentVersioning.GetVersion(reopened)).IsEqualTo(PdfVersion.Pdf17);
    }

    /// <summary>Raising to the unset version is an error.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RaisingToTheUnsetVersionFails()
    {
        using var document = PdfDocumentReader.Open(WriterControlSamples.WithOrphans(), null);

        _ = await Assert.That(() => PdfDocumentVersioning.RaiseVersion(document, default)).Throws<ArgumentException>();
    }

    /// <summary>An older document keeps one dictionary per prefix with the highest level; a lower level changes nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OlderDocumentsKeepOneDictionaryWithTheHighestLevel()
    {
        var source = OldHeaderDocument();
        using var document = PdfDocumentReader.Open(source, null);

        var first = PdfDocumentExtensionEditing.Declare(document, new(Prefix, "1.7", FirstLevel, Url));
        var same = PdfDocumentExtensionEditing.Declare(document, new(Prefix, "1.7", FirstLevel, Url));
        var lower = PdfDocumentExtensionEditing.Declare(document, new(Prefix, "1.7", LowerLevel, Url));
        var higher = PdfDocumentExtensionEditing.Declare(document, new(Prefix, "1.7", HigherLevel, Url));

        var extensions = PdfDocumentExtensionDeclarations.GetDeveloperExtensions(document);
        await Assert.That(first).IsTrue();
        await Assert.That(same).IsFalse();
        await Assert.That(lower).IsFalse();
        await Assert.That(higher).IsTrue();
        await Assert.That(extensions.Length).IsEqualTo(1);
        await Assert.That(extensions[0].ExtensionLevel).IsEqualTo(HigherLevel);
        await Assert.That(extensions[0].Url).IsEqualTo(Url);
        await Assert.That(PdfDocumentExtensionEditing.GetExtensionLevel(document, Prefix)).IsEqualTo(HigherLevel);
        await Assert.That(PdfDocumentVersioning.GetVersion(document)).IsEqualTo(PdfVersion.Pdf17);
    }

    /// <summary>The ISO_ prefix is an array; a different level is added beside the first and the same level is replaced.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task IsoPrefixKeepsAnArrayOfLevels()
    {
        using var document = PdfDocumentReader.Open(WriterControlSamples.WithOrphans(), null);

        _ = PdfDocumentExtensionEditing.Declare(document, new("ISO_", "2.0", FirstLevel, null));
        _ = PdfDocumentExtensionEditing.Declare(document, new("ISO_", "2.0", HigherLevel, null));
        var replaced = PdfDocumentExtensionEditing.Declare(document, new("ISO_", "2.0", FirstLevel, Url));

        var array = document.Catalog.GetDictionary(KnownName.Extensions)!.Get(document.Objects.Names.Intern("ISO_")).AsArray();
        await Assert.That(replaced).IsTrue();
        await Assert.That(array?.Count).IsEqualTo(TwoLevels);
        await Assert.That(PdfDocumentExtensionEditing.GetExtensionLevel(document, "ISO_")).IsEqualTo(HigherLevel);
    }

    /// <summary>A declared extension survives a whole-file save and raises the header to its base version.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DeclaredExtensionSurvivesACompactSave()
    {
        var source = OldHeaderDocument();
        using var document = PdfDocumentReader.Open(source, null);
        _ = PdfDocumentExtensionEditing.Declare(document, new(Prefix, "1.7", FirstLevel, null));

        var saved = PdfCompactWriter.Save(document.Objects, PdfCompactOptions.Classic);

        using var reopened = PdfDocumentReader.Open(saved, null);
        await Assert.That(PdfDocumentExtensionEditing.GetExtensionLevel(reopened, Prefix)).IsEqualTo(FirstLevel);
        await Assert.That(WriterStreams.Header(saved)).IsEqualTo(SampleHeader);
    }

    /// <summary>A prefix that is not a name, a base version that is not a version and a negative level are rejected.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InvalidDeclarationsAreRejected()
    {
        using var document = PdfDocumentReader.Open(WriterControlSamples.WithOrphans(), null);

        _ = await Assert.That(() => PdfDocumentExtensionEditing.Declare(document, new(string.Empty, "1.7", 1, null))).Throws<ArgumentException>();
        _ = await Assert.That(() => PdfDocumentExtensionEditing.Declare(document, new("A B", "1.7", 1, null))).Throws<ArgumentException>();
        _ = await Assert.That(() => PdfDocumentExtensionEditing.Declare(document, new(Prefix, "seven", 1, null))).Throws<ArgumentException>();
        _ = await Assert.That(() => PdfDocumentExtensionEditing.Declare(document, new(Prefix, "1.7", -1, null))).Throws<ArgumentException>();
    }

    /// <summary>Builds the sample file with an older header.</summary>
    /// <returns>The file.</returns>
    private static byte[] OldHeaderDocument() =>
        Encoding.Latin1.GetBytes(Encoding.Latin1.GetString(WriterControlSamples.WithOrphans()).Replace(SampleHeader, OldHeader, StringComparison.Ordinal));
}
