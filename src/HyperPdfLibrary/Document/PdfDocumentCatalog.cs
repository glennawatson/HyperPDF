// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Features;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>Reads document catalog entries.</summary>
public static class PdfDocumentCatalog
{
    /// <summary>The most beads read in one thread.</summary>
    private const int MaxBeads = 100_000;

    /// <summary>The change level of a certification signature that names none.</summary>
    private const int DefaultDocMdpLevel = 2;

    /// <summary>Gets the natural language of the document (the catalog's <c>/Lang</c>, ISO 32000-2 §14.9.2.2).</summary>
    /// <param name="document">The document.</param>
    /// <returns>The language tag, or <see langword="null"/> when the catalog has none or it is blank.</returns>
    public static string? GetLanguage(PdfDocument document) =>
        document.Catalog.Text("Lang") is { } language && !string.IsNullOrWhiteSpace(language) ? language : null;

    /// <summary>Gets the article threads (the catalog's <c>/Threads</c>).</summary>
    /// <param name="document">The document.</param>
    /// <returns>An immutable snapshot of the threads, read once until the next edit.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ImmutableArray<PdfThread> GetThreads(PdfDocument document) => PdfReadSnapshots.Read(document, PdfReadKind.Threads, PdfDocumentCatalog.ReadThreads);

    /// <summary>Gets the output intents (the catalog's <c>/OutputIntents</c>).</summary>
    /// <param name="document">The document.</param>
    /// <returns>The output intents.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfOutputIntent[] GetOutputIntents(PdfDocument document) => PdfDocumentCatalog.ReadOutputIntents(document.Catalog);

    /// <summary>Gets the output intents of a page (PDF 2.0).</summary>
    /// <param name="document">The document.</param>
    /// <param name="page">The page.</param>
    /// <returns>The output intents.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="page"/> is null.</exception>
    public static PdfOutputIntent[] GetOutputIntents(PdfDocument document, PdfPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        return PdfDocumentCatalog.ReadOutputIntents(page.Dictionary);
    }

    /// <summary>Gets the permissions (<c>/Perms</c>) and the legal attestation (<c>/Legal</c>).</summary>
    /// <param name="document">The document.</param>
    /// <returns>The permissions, or <see langword="null"/> when the catalog has neither entry.</returns>
    public static PdfPermissions? GetPermissions(PdfDocument document)
    {
        var perms = document.Catalog.Dict("Perms");
        var legal = document.Catalog.Dict("Legal");
        if (perms is null && legal is null)
        {
            return null;
        }

        var counts = new Dictionary<string, long>(StringComparer.Ordinal);
        for (var i = 0; legal is not null && i < legal.Count; i++)
        {
            if (StoreReading.Resolve(document.Objects, legal.GetValueAt(i)) is { IsNumber: true } count)
            {
                counts[document.Objects.Names.GetString(legal.GetKeyAt(i))] = count.AsInteger();
            }
        }

        var certification = perms?.Dict("DocMDP");
        return new(certification is not null, certification is null ? null : PdfDocumentCatalog.ReadDocMdpLevel(certification), perms?.Dict("UR3") is not null, counts, legal?.Text("Attestation"));
    }

    /// <summary>Gets the page-piece data of the catalog (<c>/PieceInfo</c>).</summary>
    /// <param name="document">The document.</param>
    /// <returns>One entry per application.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ImmutableArray<PdfPieceData> GetPieceInfo(PdfDocument document) => PdfDocumentCatalog.GetPieceInfo(document, document.Catalog);

    /// <summary>Gets the page-piece data of a page.</summary>
    /// <param name="document">The document.</param>
    /// <param name="page">The page.</param>
    /// <returns>One entry per application.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="page"/> is null.</exception>
    public static ImmutableArray<PdfPieceData> GetPieceInfo(PdfDocument document, PdfPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        return PdfDocumentCatalog.GetPieceInfo(document, page.Dictionary);
    }

    /// <summary>Gets the page-piece data of any dictionary that has <c>/PieceInfo</c> (a page, a form XObject or the catalog).</summary>
    /// <param name="document">The document.</param>
    /// <param name="owner">The dictionary.</param>
    /// <returns>An immutable snapshot with one entry per application whose value is a data dictionary, read once until the next edit.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> is null.</exception>
    public static ImmutableArray<PdfPieceData> GetPieceInfo(PdfDocument document, PdfDictionary owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return PdfReadSnapshots.Read(document, PdfReadKind.PieceInfo, owner, PdfDocumentCatalog.ReadPieceInfo);
    }

    /// <summary>Reads the page-piece data of a dictionary.</summary>
    /// <param name="document">The document.</param>
    /// <param name="owner">The dictionary.</param>
    /// <returns>One entry per application whose value is a data dictionary.</returns>
    internal static ImmutableArray<PdfPieceData> ReadPieceInfo(PdfDocument document, PdfDictionary owner)
    {
        if (owner.Dict("PieceInfo") is not { } info)
        {
            return [];
        }

        var pieces = new List<PdfPieceData>(info.Count);
        for (var i = 0; i < info.Count; i++)
        {
            // ISO 32000-2 Table 350: each value is a data dictionary; anything else carries no data to report.
            if (StoreReading.Resolve(document.Objects, info.GetValueAt(i)).AsDictionary() is not { } data)
            {
                continue;
            }

            var modified = data.Value("LastModified");
            pieces.Add(new(document.Objects.Names.GetString(info.GetKeyAt(i)), modified.Kind == PdfKind.String ? PdfDate.Parse(modified.AsStringBytes()) : null, data.Dict("Private")));
        }

        return [.. pieces];
    }

    /// <summary>Reads the article threads (the catalog's <c>/Threads</c>).</summary>
    /// <param name="document">The document.</param>
    /// <returns>The threads.</returns>
    internal static ImmutableArray<PdfThread> ReadThreads(PdfDocument document)
    {
        var list = document.Catalog.Array("Threads");
        var threads = new List<PdfThread>(list?.Count ?? 0);
        for (var i = 0; list is not null && i < list.Count; i++)
        {
            if (list.GetDictionary(i) is { } thread)
            {
                threads.Add(new(thread.Dict("I")?.GetText(KnownName.Title), PdfDocumentCatalog.ReadBeads(document, thread.Dict("F")), thread));
            }
        }

        return [.. threads];
    }

    /// <summary>Reads output intents from a catalog or page.</summary>
    /// <param name="owner">The dictionary holding <c>/OutputIntents</c>.</param>
    /// <returns>The output intents.</returns>
    private static PdfOutputIntent[] ReadOutputIntents(PdfDictionary owner)
    {
        var list = owner.Array("OutputIntents");
        var intents = new List<PdfOutputIntent>(list?.Count ?? 0);
        for (var i = 0; list is not null && i < list.Count; i++)
        {
            if (list.GetDictionary(i) is not { } intent)
            {
                continue;
            }

            var profile = intent.Stream("DestOutputProfile");
            intents.Add(new(
        intent.NameText("S") ?? string.Empty,
        intent.Text("OutputCondition"),
        intent.Text("OutputConditionIdentifier"),
        intent.Text("RegistryName"),
        intent.Text("Info"),
        profile?.Dictionary.Int(
        "N",
        0) ?? 0,
        profile));
        }

        return [.. intents];
    }

    /// <summary>Reads the change level of a certification signature from its transform parameters.</summary>
    /// <param name="signature">The <c>/DocMDP</c> signature dictionary.</param>
    /// <returns>The level, or null when the signature has no DocMDP reference.</returns>
    private static int? ReadDocMdpLevel(PdfDictionary signature)
    {
        var references = signature.Array("Reference");
        for (var i = 0; references is not null && i < references.Count; i++)
        {
            if (references.GetDictionary(i) is { } reference && reference.NameText("TransformMethod") == "DocMDP")
            {
                return reference.Dict("TransformParams")?.Int("P", PdfDocumentCatalog.DefaultDocMdpLevel) ?? PdfDocumentCatalog.DefaultDocMdpLevel;
            }
        }

        return null;
    }

    /// <summary>Reads the beads of a thread by following <c>/N</c> from the first bead until the ring closes.</summary>
    /// <param name="document">The document.</param>
    /// <param name="first">The first bead (<c>/F</c>), or null.</param>
    /// <returns>The beads.</returns>
    private static ImmutableArray<PdfBead> ReadBeads(PdfDocument document, PdfDictionary? first)
    {
        var beads = new List<PdfBead>();
        var visited = new HashSet<PdfDictionary>(ReferenceEqualityComparer.Instance);
        for (var bead = first; bead is not null && beads.Count < PdfDocumentCatalog.MaxBeads && visited.Add(bead); bead = bead.GetDictionary(KnownName.N))
        {
            var page = bead.GetRaw(KnownName.P);
            beads.Add(new(page.IsReference ? PdfDocumentPages.GetPageIndex(document, page.AsReference()) : -1, bead.Rect("R")));
        }

        return [.. beads];
    }
}
