// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Extensions;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Document;

/// <summary>Tests for developer extensions, unknown entries and their survival through saving.</summary>
public sealed class ExtensionTests
{
    /// <summary>The extension level of the ADBE extension.</summary>
    private const int AdobeLevel = 8;

    /// <summary>The number of declared extensions.</summary>
    private const int ExtensionCount = 2;

    /// <summary>The number of unknown entries in the sample catalog and page.</summary>
    private const int UnknownCount = 5;

    /// <summary>The marker key added by the writer tests.</summary>
    private const string MarkerKey = "SavedMarker";

    /// <summary>The catalog entries of the sample document.</summary>
    private const string CatalogEntries =
            "/Extensions << /ADBE << /BaseVersion /1.7 /ExtensionLevel 8 >> /XYZ << /BaseVersion /2.0 /ExtensionLevel 1 /URL (http://x) >> >> "
            + "/ADBE_Thing 4 0 R /Odd 99 0 R /Plain 5 /XYZ_Other 4 0 R";

    /// <summary>The page entries of the sample document.</summary>
    private const string PageEntries = "/XYZ_Info << /A 1 >>";

    /// <summary>A declared developer key in the catalog.</summary>
    private const string DeclaredKey = "ADBE_Thing";

    /// <summary>A developer key on the page.</summary>
    private const string PageKey = "XYZ_Info";

    /// <summary>Developer extensions are read with their level and URL.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DeveloperExtensionsAreRead()
    {
        using var document = Open();
        var extensions = PdfDocumentExtensionDeclarations.GetDeveloperExtensions(document);
        await Assert.That(extensions.Length).IsEqualTo(ExtensionCount);
        await Assert.That(extensions[0].Prefix).IsEqualTo("ADBE");
        await Assert.That(extensions[0].BaseVersion).IsEqualTo("1.7");
        await Assert.That(extensions[0].ExtensionLevel).IsEqualTo(AdobeLevel);
        await Assert.That(extensions[1].Url).IsEqualTo("http://x");
    }

    /// <summary>Unknown keys are listed with whether they read cleanly, and known keys are not.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnknownEntriesDistinguishExtensionsFromDamage()
    {
        using var document = Open();
        var entries = PdfDocumentExtensionDeclarations.FindUnknownEntries(document);
        await Assert.That(entries.Length).IsEqualTo(UnknownCount);
        await Assert.That(Find(entries, DeclaredKey).ParsedCleanly).IsTrue();
        await Assert.That(Find(entries, DeclaredKey).HasDeveloperPrefix).IsTrue();
        await Assert.That(Find(entries, DeclaredKey).IsDeclared).IsTrue();
        await Assert.That(Find(entries, "Odd").ParsedCleanly).IsFalse();
        await Assert.That(Find(entries, "Odd").HasDeveloperPrefix).IsFalse();
        await Assert.That(Find(entries, "Plain").ValueKind).IsEqualTo(PdfKind.Integer);
        await Assert.That(Find(entries, PageKey).Owner).IsEqualTo(PdfEntryOwner.Page);
        await Assert.That(Find(entries, PageKey).PageIndex).IsEqualTo(0);
        await Assert.That(PdfDocumentExtensionDeclarations.FindUnknownEntries(document, PdfDocumentPages.GetPage(document, 0)).Length).IsEqualTo(1);
        await Assert.That(Find(entries, "XYZ_Other").IsDeclared).IsTrue();
    }

    /// <summary>Saving incrementally and compactly, with and without object streams, keeps the unknown keys and their values.</summary>
    /// <param name="mode">The save mode.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("incremental")]
    [Arguments("compact-streams")]
    [Arguments("compact-classic")]
    public async Task UnknownKeysSurviveSaving(string mode)
    {
        var saved = Save(StructureDocuments.Build(CatalogEntries, PageEntries, "<< /Kept true >>", "1"), mode);
        using var reopened = PdfDocumentReader.Open(saved, null);
        var entries = PdfDocumentExtensionDeclarations.FindUnknownEntries(reopened);
        var names = reopened.Objects.Names;
        var pageInfo = PdfDocumentPages.GetPage(reopened, 0).Dictionary.GetDictionary(names.Intern(PageKey))!;
        var thing = reopened.Catalog.GetDictionary(names.Intern(DeclaredKey))!;

        // Compaction drops a key whose reference leads to no object; an incremental update keeps every key.
        var keepsDanglingKey = mode == "incremental";
        await Assert.That(entries.Length).IsEqualTo(keepsDanglingKey ? UnknownCount + 1 : UnknownCount);
        await Assert.That(Array.Exists(entries, static entry => entry.Key == "Odd")).IsEqualTo(keepsDanglingKey);
        await Assert.That(Find(entries, DeclaredKey).ParsedCleanly).IsTrue();
        await Assert.That(Find(entries, MarkerKey).ValueKind).IsEqualTo(PdfKind.Integer);
        await Assert.That(pageInfo.GetInt32(names.Intern("A"))).IsEqualTo(1);
        await Assert.That(thing.GetBoolean(names.Intern("Kept"))).IsTrue();
    }

    /// <summary>Finds an unknown entry by key.</summary>
    /// <param name="entries">The entries.</param>
    /// <param name="key">The key.</param>
    /// <returns>The entry.</returns>
    private static PdfUnknownEntry Find(PdfUnknownEntry[] entries, string key) => Array.Find(entries, entry => entry.Key == key)!;

    /// <summary>Opens the sample document.</summary>
    /// <returns>The document.</returns>
    private static PdfDocument Open() => PdfDocumentReader.Open(StructureDocuments.Build(CatalogEntries, PageEntries, "<< /Kept true >>", "1"), null);

    /// <summary>Opens the file, adds a marker key to the catalog and saves it.</summary>
    /// <param name="original">The file.</param>
    /// <param name="mode">The save mode.</param>
    /// <returns>The saved file.</returns>
    private static byte[] Save(byte[] original, string mode)
    {
        using var store = StoreOpening.Open(original, null);
        var catalog = store.Catalog.Clone();
        catalog.Set(store.Names.Intern(MarkerKey), PdfValue.FromInteger(1));
        StoreEditing.Replace(store, store.Trailer.GetRaw(KnownName.Root).AsReference(), PdfValue.FromDictionary(catalog));
        return mode switch
        {
            "incremental" => PdfIncrementalWriter.Save(store),
            "compact-streams" => PdfCompactWriter.Save(store, PdfCompactOptions.Default),
            _ => PdfCompactWriter.Save(store, PdfCompactOptions.Classic),
        };
    }
}
