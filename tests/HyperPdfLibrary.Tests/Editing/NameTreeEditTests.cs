// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Editing;

/// <summary>Tests for <see cref="PdfDocumentNameTrees"/>.</summary>
public sealed class NameTreeEditTests
{
    /// <summary>Pages in the test document.</summary>
    private const int Pages = 2;

    /// <summary>Entries added in bulk, enough for three levels of nodes.</summary>
    private const int BulkCount = 3000;

    /// <summary>Entries added one at a time.</summary>
    private const int OneByOneCount = 200;

    /// <summary>The step between names, so single adds land between bulk names.</summary>
    private const int Gap = 2;

    /// <summary>The entries left after every fourth bulk name is removed.</summary>
    private const int AfterRemovals = 2450;

    /// <summary>A number that is not a tree kind.</summary>
    private const int UnknownKind = 99;

    /// <summary>A bulk add, single adds and removes keep the tree balanced, sorted and limited; every save reopens the same.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task EditsKeepTheTreeBalanced(CancellationToken cancellationToken)
    {
        using var document = PdfDocumentReader.Open(TestPdf.Create(Pages), null);
        var bulk = new List<PdfNameTreeEntry>();
        for (var i = 0; i < BulkCount; i++)
        {
            bulk.Add(new(Name(i * Gap), Destination(document)));
        }

        await PdfDocumentNameTrees.SetEntriesAsync(document, PdfNameTreeKind.Destinations, bulk, cancellationToken);
        await AssertBalanced(document, BulkCount);

        for (var i = 0; i < OneByOneCount; i++)
        {
            PdfDocumentNameTrees.SetEntry(document, PdfNameTreeKind.Destinations, Name((i * Gap * Gap) + 1), Destination(document));
        }

        await AssertBalanced(document, BulkCount + OneByOneCount);
        for (var i = 0; i < BulkCount; i += Gap * Gap)
        {
            await Assert.That(PdfDocumentNameTrees.RemoveEntry(document, PdfNameTreeKind.Destinations, Name(i * Gap))).IsTrue();
        }

        await AssertBalanced(document, AfterRemovals);
        await Assert.That(PdfDocumentNameTrees.RemoveEntry(document, PdfNameTreeKind.Destinations, "absent")).IsFalse();

        var incremental = EditChecks.SaveIncremental(document);
        var compact = EditChecks.SaveCompact(document);
        foreach (var file in new[] { incremental, compact })
        {
            using var reopened = PdfDocumentReader.Open(file, null);
            await AssertBalanced(reopened, AfterRemovals);
            await Assert.That(PdfDocumentNameTrees.GetEntries(reopened, PdfNameTreeKind.Destinations).Select(static entry => entry.Name))
                .IsEquivalentTo(PdfDocumentNameTrees.GetEntries(document, PdfNameTreeKind.Destinations).Select(static entry => entry.Name));
        }

        await EditChecks.AssertQpdfCleanAsync("nametree-incremental", incremental, cancellationToken);
        await EditChecks.AssertQpdfCleanAsync("nametree-compact", compact, cancellationToken);
    }

    /// <summary>Replacing a name keeps one entry with the new value.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SettingAnExistingNameReplacesIt()
    {
        using var document = PdfDocumentReader.Open(TestPdf.Create(Pages), null);
        PdfDocumentNameTrees.SetEntry(document, PdfNameTreeKind.Destinations, "chapter", PdfValue.FromInteger(1));
        PdfDocumentNameTrees.SetEntry(document, PdfNameTreeKind.Destinations, "chapter", PdfValue.FromInteger(Gap));

        var entries = PdfDocumentNameTrees.GetEntries(document, PdfNameTreeKind.Destinations);
        await Assert.That(entries.Count).IsEqualTo(1);
        await Assert.That(entries[0].Value.AsInt32()).IsEqualTo(Gap);
    }

    /// <summary>Removing the last name removes the tree, and undo brings each edit back.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UndoRestoresEachEdit()
    {
        using var document = PdfDocumentReader.Open(TestPdf.Create(Pages), null);
        PdfDocumentNameTrees.SetEntry(document, PdfNameTreeKind.Destinations, "one", PdfValue.FromInteger(1));
        PdfDocumentNameTrees.SetEntry(document, PdfNameTreeKind.Destinations, "two", PdfValue.FromInteger(Gap));
        await Assert.That(PdfDocumentNameTrees.RemoveEntry(document, PdfNameTreeKind.Destinations, "one")).IsTrue();
        await Assert.That(PdfDocumentNameTrees.RemoveEntry(document, PdfNameTreeKind.Destinations, "two")).IsTrue();
        await Assert.That(document.Catalog.GetDictionary(KnownName.Names)).IsNull();

        await Assert.That(PdfDocumentEditing.Undo(document)).IsTrue();
        await Assert.That(PdfDocumentNameTrees.GetEntries(document, PdfNameTreeKind.Destinations).Count).IsEqualTo(1);
        await Assert.That(PdfDocumentEditing.Undo(document)).IsTrue();
        await Assert.That(PdfDocumentNameTrees.GetEntries(document, PdfNameTreeKind.Destinations).Count).IsEqualTo(Gap);
        await Assert.That(PdfDocumentEditing.Undo(document)).IsTrue();
        await Assert.That(PdfDocumentNameTrees.GetEntries(document, PdfNameTreeKind.Destinations).Count).IsEqualTo(1);
        await Assert.That(PdfDocumentEditing.Undo(document)).IsTrue();
        await Assert.That(PdfDocumentNameTrees.GetEntries(document, PdfNameTreeKind.Destinations)).IsEmpty();
        await Assert.That(PdfDocumentEditing.Redo(document)).IsTrue();
        await Assert.That(PdfDocumentNameTrees.GetEntries(document, PdfNameTreeKind.Destinations).Count).IsEqualTo(1);
    }

    /// <summary>A tree from another program, with no limits and a long flat list, is rebalanced.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task RebalanceFixesAFlatTree(CancellationToken cancellationToken)
    {
        using var document = PdfDocumentReader.Open(TestPdf.Create(Pages), null);
        var flat = new PdfArray(document.Objects);
        for (var i = 0; i < BulkCount; i++)
        {
            flat.Add(PdfValue.FromString(PdfText.Encode(Name(i))));
            flat.Add(PdfValue.FromInteger(i));
        }

        var root = new PdfDictionary(document.Objects);
        root.Set(KnownName.Names, PdfValue.FromArray(flat));
        var names = new PdfDictionary(document.Objects);
        names.Set(KnownName.Dests, PdfValue.FromDictionary(root));
        var catalog = document.Catalog.Clone();
        catalog.Set(KnownName.Names, PdfValue.FromDictionary(names));
        var transaction = PdfDocumentEditing.BeginEdit(document, "Seed");
        PdfPageTreeWriter.ReplaceCatalog(transaction, document.Objects, catalog);
        transaction.Commit();
        await Assert.That(NameTreeInvariants.Check(Root(document), out _)).IsNotEmpty();

        await PdfDocumentNameTrees.RebalanceAsync(document, PdfNameTreeKind.Destinations, cancellationToken);

        await AssertBalanced(document, BulkCount);
    }

    /// <summary>A tree kind outside the enumeration is refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OutOfRangeKindIsRefused()
    {
        using var document = PdfDocumentReader.Open(TestPdf.Create(Pages), null);

        await Assert.That(() => PdfDocumentNameTrees.SetEntry(document, (PdfNameTreeKind)UnknownKind, "a", default)).Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>Makes a destination value that fits the first page.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The destination array.</returns>
    private static PdfValue Destination(PdfDocument document) =>
        PdfValue.FromArray(new(document.Objects, [PdfValue.FromReference(PdfDocumentPages.GetPage(document, 0).Id), PdfValue.FromName(KnownName.Fit)]));

    /// <summary>Makes the name of the entry at a position.</summary>
    /// <param name="index">The position.</param>
    /// <returns>A name that sorts in position order.</returns>
    private static string Name(int index) => string.Create(CultureInfo.InvariantCulture, $"n{index:D6}");

    /// <summary>Gets the root of the destinations tree.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The root, or <see langword="null"/>.</returns>
    private static PdfDictionary? Root(PdfDocument document) =>
        document.Catalog.GetDictionary(KnownName.Names)?.GetDictionary(document.Objects.Names.Intern("Dests"));

    /// <summary>Checks that the destinations tree is well formed and holds a number of entries.</summary>
    /// <param name="document">The document.</param>
    /// <param name="expected">The number of entries.</param>
    /// <returns>A task.</returns>
    private static async Task AssertBalanced(PdfDocument document, int expected)
    {
        var problems = NameTreeInvariants.Check(Root(document), out var count);
        await Assert.That(problems).IsEmpty();
        await Assert.That(count).IsEqualTo(expected);
        await Assert.That(PdfDocumentNameTrees.GetEntries(document, PdfNameTreeKind.Destinations).Count).IsEqualTo(expected);
    }
}
