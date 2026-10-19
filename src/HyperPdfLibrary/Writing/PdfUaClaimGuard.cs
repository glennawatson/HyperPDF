// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Accessibility;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Metadata;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.PdfUa;

namespace HyperPdfLibrary.Writing;

/// <summary>
/// Checks the PDF/UA-1 identification of a document when it is saved, against the rules of ISO 14289-1:2014 clauses 5 and 7 that the
/// library tests. Every writer calls it
/// before it writes: a document whose XMP claims PDF/UA-1 is checked, the problems a repair can fix are fixed in the
/// document in memory as one undoable edit (a missing tab order, display title, marked flag, language taken from the file,
/// Unicode map, annotation tag or dangling tag), and the document is checked again. A problem that cannot be fixed
/// stops the save with the reason, and nothing is written, unless the caller chose to remove the claim
/// (<see cref="PdfSaveReport.UnmetUaClaim"/>). A claim that still holds after an edit is kept as it was. A PDF/UA-2 claim
/// (ISO 14289-2:2024) is checked the same way against the rules the library tests for part 2.
/// </summary>
internal static class PdfUaClaimGuard
{
    /// <summary>The undo label of the repairs.</summary>
    private const string RepairLabel = "Repair PDF/UA claim";

    /// <summary>The undo label of the claim's removal.</summary>
    private const string RemoveLabel = "Remove PDF/UA claim";

    /// <summary>The prefix of the PDF/UA identification schema.</summary>
    private const string IdentificationPrefix = "pdfuaid";

    /// <summary>The most distinct requirements named in a refusal.</summary>
    private const int MaxNamed = 6;

    /// <summary>Gets the PDF/UA identification namespace as it appears in a UTF-8 packet.</summary>
    private static ReadOnlySpan<byte> IdentificationNamespace => "http://www.aiim.org/pdfua/ns/id/"u8;

    /// <summary>Gets the start of an XML numeric character reference, which could spell the namespace out.</summary>
    private static ReadOnlySpan<byte> CharacterReference => "&#"u8;

    /// <summary>Gets the identification properties that make up a claim: part, amendment, corrigenda and revision.</summary>
    private static XmpChange[] ClaimRemoval =>
    [
        Removal("part"),
        Removal("amd"),
        Removal("corr"),
        Removal("rev"),
    ];

    /// <summary>Determines whether the document being saved claims PDF/UA-1 or PDF/UA-2, so its content must be checked.</summary>
    /// <param name="store">The objects being written.</param>
    /// <returns><see langword="true"/> when the catalog's XMP names PDF/UA part 1 or part 2.</returns>
    internal static bool IsClaimed(PdfObjectStore store) => ClaimedPart(store) is not null;

    /// <summary>Determines whether the document being saved claims PDF/UA-1, which the encryption rules treat on their own.</summary>
    /// <param name="store">The objects being written.</param>
    /// <returns><see langword="true"/> when the catalog's XMP names PDF/UA part 1.</returns>
    internal static bool IsPart1Claimed(PdfObjectStore store) => ClaimedPart(store) == PdfUaClaim.Part1;

    /// <summary>Gets the PDF/UA part the document claims.</summary>
    /// <param name="store">The objects being written.</param>
    /// <returns>1 or 2, or <see langword="null"/> when the catalog's XMP names neither part.</returns>
    internal static int? ClaimedPart(PdfObjectStore store)
    {
        if (store.Catalog.GetStream(KnownName.Metadata) is not { } stream || NamesNoPdfUa(stream))
        {
            return null;
        }

        var packet = stream.DecodeOrEmpty();
        if (packet.Length == 0)
        {
            return null;
        }

        return XmpParser.Parse(packet, CancellationToken.None).PdfUaPart is (PdfUaClaim.Part1 or PdfUaClaim.Part2) and var part ? part : null;
    }

    /// <summary>
    /// Checks the document before it is written and repairs what can be repaired. Does nothing for a document that does
    /// not claim PDF/UA-1.
    /// </summary>
    /// <param name="store">The objects being written.</param>
    /// <param name="wholeFile">Whether the save writes every object; an update that writes only edited objects, with no edits, writes nothing and is not checked.</param>
    /// <param name="report">Receives the check and holds the choice for an unmet claim, or <see langword="null"/>, which refuses.</param>
    /// <param name="cancellationToken">Stops the check.</param>
    /// <exception cref="PdfException">The claim cannot be kept and the save was not told to remove it (<see cref="PdfError.ClaimUnmet"/>); nothing was written.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    internal static void Apply(PdfObjectStore store, bool wholeFile, PdfSaveReport? report, CancellationToken cancellationToken)
    {
        report?.UaClaim = null;
        var edited = StoreEditing.HasChanges(store);
        if ((!wholeFile && !edited) || ClaimedPart(store) is not { } part)
        {
            return;
        }

        var found = PdfUaPreflight.Check(store, edited, part, cancellationToken);
        if (found.Count == 0)
        {
            report?.UaClaim = new([]);
            return;
        }

        Repair(store, found, cancellationToken);
        var remaining = PdfUaPreflight.Check(store, edited, part, cancellationToken);
        Settle(store, report, new(Describe(found, remaining, part)), part, remaining.Count > 0);
    }

    /// <summary>
    /// Determines from the packet's bytes alone that it cannot claim PDF/UA, so a document without a claim is not parsed or decoded on
    /// every save. A claim needs the identification namespace in the packet; a packet that is filtered, encrypted, not UTF-8
    /// or written with character references is left to the full read.
    /// </summary>
    /// <param name="stream">The catalog's metadata stream.</param>
    /// <returns><see langword="true"/> when the packet is plain, readable bytes that never name the PDF/UA identification namespace.</returns>
    private static bool NamesNoPdfUa(PdfStream stream)
    {
        if (stream.IsEncrypted || stream.Dictionary.ContainsKey(KnownName.Filter))
        {
            return false;
        }

        using var raw = stream.LeaseRawData();
        var packet = raw.Span;
        return packet.IndexOf(IdentificationNamespace) < 0 && packet.IndexOf((byte)0) < 0 && packet.IndexOf(CharacterReference) < 0;
    }

    /// <summary>Records the check, then keeps, removes or refuses the claim.</summary>
    /// <param name="store">The objects being written.</param>
    /// <param name="report">Receives the check and holds the choice, or <see langword="null"/>, which refuses.</param>
    /// <param name="check">The check, with the problems before and after the repair.</param>
    /// <param name="part">The claimed PDF/UA part.</param>
    /// <param name="unmet">Whether a problem the repair could not fix remains.</param>
    /// <exception cref="PdfException">The claim cannot be kept and the save was not told to remove it.</exception>
    private static void Settle(PdfObjectStore store, PdfSaveReport? report, PdfUaClaimCheck check, int part, bool unmet)
    {
        report?.UaClaim = check;
        if (!unmet)
        {
            return;
        }

        if (report?.UnmetUaClaim != PdfUaUnmetClaimAction.RemoveClaim)
        {
            throw new PdfException(PdfError.ClaimUnmet, Refusal(check, part));
        }

        RemoveClaim(store, part);
        report.UaClaim = check with { Removed = true };
    }

    /// <summary>Makes the change that removes one identification property.</summary>
    /// <param name="localName">The property's local name.</param>
    /// <returns>The change.</returns>
    private static XmpChange Removal(string localName) => new(new(IdentificationPrefix, XmpMetadata.PdfUaIdNamespace, localName, XmpShape.Simple), string.Empty);

    /// <summary>Repairs what can be repaired, in one undoable edit.</summary>
    /// <param name="store">The objects being written.</param>
    /// <param name="found">The findings of the first check.</param>
    /// <param name="cancellationToken">Stops the repair.</param>
    private static void Repair(PdfObjectStore store, List<PdfUaFinding> found, CancellationToken cancellationToken)
    {
        var wanted = new HashSet<PdfUaRequirement>();
        foreach (var finding in found)
        {
            _ = wanted.Add(finding.Requirement);
        }

        var transaction = PdfGuardTransactions.JoinOrBegin(store, RepairLabel, out var owned);
        try
        {
            PdfUaRepairs.Run(store, transaction, wanted, cancellationToken);
        }
        catch
        {
            if (owned)
            {
                transaction.Rollback();
            }

            throw;
        }

        if (owned)
        {
            transaction.Commit();
        }
    }

    /// <summary>Describes the findings in plain words, marking the ones the repair fixed.</summary>
    /// <param name="found">The findings of the first check.</param>
    /// <param name="remaining">The findings of the check after the repair.</param>
    /// <param name="part">The claimed PDF/UA part, which picks the clause named for each problem.</param>
    /// <returns>One problem for each distinct finding, in requirement order.</returns>
    private static PdfUaProblem[] Describe(List<PdfUaFinding> found, List<PdfUaFinding> remaining, int part)
    {
        var still = new HashSet<PdfUaFinding>(remaining);
        List<PdfUaFinding> distinct = [.. new HashSet<PdfUaFinding>(found)];
        distinct.Sort(static (a, b) => a.Requirement != b.Requirement ? a.Requirement.CompareTo(b.Requirement) : a.PageIndex.CompareTo(b.PageIndex));
        var problems = new List<PdfUaProblem>(distinct.Count);
        foreach (var finding in distinct)
        {
            problems.Add(new(finding.Requirement, PdfUaRequirements.Clause(finding.Requirement, part), PdfUaRequirements.Message(finding.Requirement), finding.PageIndex, !still.Contains(finding)));
        }

        return [.. problems];
    }

    /// <summary>Writes the reason a save is refused.</summary>
    /// <param name="check">The check, with its unrepaired problems.</param>
    /// <param name="part">The claimed PDF/UA part.</param>
    /// <returns>The reason in plain words.</returns>
    private static string Refusal(PdfUaClaimCheck check, int part)
    {
        var text = new StringBuilder(
            $"This file's metadata names PDF/UA-{part}, and the checks below found problems that cannot be fixed automatically. "
            + $"It was not saved, because the saved file would still carry the PDF/UA-{part} identification. ");
        var named = new HashSet<PdfUaRequirement>();
        foreach (var problem in check.GetUnrepaired())
        {
            if (named.Count < MaxNamed && named.Add(problem.Requirement))
            {
                _ = text.Append("Clause ").Append(problem.Clause).Append(": ").Append(problem.Message).Append(' ');
            }
        }

        _ = text.Append("Fix these problems, or save the file without the PDF/UA claim.");
        return text.ToString();
    }

    /// <summary>Removes the PDF/UA identification from the catalog's XMP packet, as one undoable edit.</summary>
    /// <param name="store">The objects being written.</param>
    /// <param name="part">The claimed PDF/UA part.</param>
    /// <exception cref="PdfException">The packet cannot be edited, so the claim cannot be removed.</exception>
    private static void RemoveClaim(PdfObjectStore store, int part)
    {
        var raw = store.Catalog.GetRaw(KnownName.Metadata);
        if (!raw.IsReference || StoreReading.Resolve(store, raw).AsStream() is not { } stream
            || !XmpEditor.TryApply(stream.DecodeToArray(), ClaimRemoval, out var packet) || packet is null)
        {
            throw new PdfException(
                PdfError.ClaimUnmet,
                $"The document's metadata names PDF/UA-{part} and the checks found problems that cannot be fixed, "
                + "and its XMP metadata cannot be edited to remove the identification. It was not saved, because the saved file would still carry it.");
        }

        // The packet is written uncompressed, as XMP is meant to be readable by tools that do not parse PDF.
        var dictionary = stream.Dictionary.Clone();
        _ = dictionary.Remove(KnownName.Filter);
        _ = dictionary.Remove(KnownName.DecodeParms);
        _ = dictionary.Remove(KnownName.Length);
        _ = dictionary.Remove(KnownName.DL);
        var transaction = PdfGuardTransactions.JoinOrBegin(store, RemoveLabel, out var owned);
        try
        {
            transaction.Replace(raw.AsReference(), PdfValue.FromStream(new(dictionary, packet)));
        }
        catch
        {
            if (owned)
            {
                transaction.Rollback();
            }

            throw;
        }

        if (owned)
        {
            transaction.Commit();
        }
    }
}
