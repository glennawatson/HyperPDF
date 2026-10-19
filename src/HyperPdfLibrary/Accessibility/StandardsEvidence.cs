// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Accessibility;

/// <summary>What HyperPdf has found in one document, read only when a rule needs it. The owner disposes it to return the working storage.</summary>
internal ref struct StandardsEvidence : IDisposable
{
    /// <summary>The document.</summary>
    private readonly PdfDocument _document;

    /// <summary>The level of Well-Tagged PDF findings that count: findings of a higher level do not break a lower one.</summary>
    private readonly PdfWtpdfProfile _level;

    /// <summary>Cancels the reads.</summary>
    private readonly CancellationToken _cancellationToken;

    /// <summary>The accessibility report, once read.</summary>
    private PdfAccessibilityReport? _report;

    /// <summary>The Well-Tagged PDF report, once read.</summary>
    private PdfWtpdfReport? _wtpdf;

    /// <summary>The broken parent-child pairs as ISO/TS 32005 Table 5 reads the tree, once read.</summary>
    private bool[]? _tablePairs;

    /// <summary>The broken parent-child pairs as the Annex L profiles read the tree, once read.</summary>
    private bool[]? _annexPairs;

    /// <summary>Initializes a new instance of the <see cref="StandardsEvidence"/> struct.</summary>
    /// <param name="document">The document.</param>
    /// <param name="level">The level of Well-Tagged PDF findings that count.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    internal StandardsEvidence(PdfDocument document, PdfWtpdfProfile level, CancellationToken cancellationToken)
    {
        _document = document;
        _level = level;
        _cancellationToken = cancellationToken;
    }

    /// <summary>Returns the working storage.</summary>
    public void Dispose()
    {
        Release(ref _tablePairs);
        Release(ref _annexPairs);
    }

    /// <summary>Determines whether a link's checks fail the document.</summary>
    /// <param name="link">The link.</param>
    /// <returns><see langword="true"/> when one of the link's report codes or Well-Tagged PDF clauses is found.</returns>
    internal bool Fails(StandardsRuleLink link)
    {
        _report ??= PdfAccessibilityReader.Read(_document, _cancellationToken);
        foreach (var code in link.Codes)
        {
            if (_report.GetCount(code) > 0)
            {
                return true;
            }
        }

        return link.WtpdfClauses.Length > 0 && FailsWtpdf(link.WtpdfClauses);
    }

    /// <summary>Determines whether the file lacks a Well-Tagged PDF declaration.</summary>
    /// <param name="accessibility">Whether the accessibility declaration is meant; otherwise the reuse declaration.</param>
    /// <returns><see langword="true"/> when the declaration is missing.</returns>
    internal bool LacksDeclaration(bool accessibility)
    {
        _wtpdf ??= WtpdfScan.Check(_document, _cancellationToken);
        return accessibility ? !_wtpdf.DeclaresAccessibility : !_wtpdf.DeclaresReuse;
    }

    /// <summary>Determines whether the structure tree breaks a parent-child pair.</summary>
    /// <param name="pairId">The pair's number.</param>
    /// <param name="annexL">Whether to read the tree as the Annex L profiles do.</param>
    /// <returns><see langword="true"/> when the pair is broken.</returns>
    internal bool IsPairBroken(int pairId, bool annexL)
    {
        if (annexL)
        {
            _annexPairs ??= CollectPairs(true);
            return _annexPairs[pairId];
        }

        _tablePairs ??= CollectPairs(false);
        return _tablePairs[pairId];
    }

    /// <summary>Returns a rented flag array.</summary>
    /// <param name="flags">The array, or <see langword="null"/>; it is set to <see langword="null"/>.</param>
    private static void Release(ref bool[]? flags)
    {
        if (flags is null)
        {
            return;
        }

        ArrayPool<bool>.Shared.Return(flags, clearArray: true);
        flags = null;
    }

    /// <summary>Finds the parent-child pairs the structure tree breaks.</summary>
    /// <param name="annexL">Whether to read the tree as the Annex L profiles do.</param>
    /// <returns>A rented array that holds a flag for each pair; the caller returns it.</returns>
    private readonly bool[] CollectPairs(bool annexL)
    {
        var pairs = ArrayPool<bool>.Shared.Rent(StandardsRulePlans.PairCount);
        var flags = pairs.AsSpan(0, StandardsRulePlans.PairCount);
        flags.Clear();
        StandardsContainmentRules.Collect(PdfDocumentTagged.GetStructureTree(_document), flags, annexL, _cancellationToken);
        return pairs;
    }

    /// <summary>Determines whether a Well-Tagged PDF finding with one of the clauses counts at this level.</summary>
    /// <param name="clauses">The clauses.</param>
    /// <returns><see langword="true"/> when there is one.</returns>
    private bool FailsWtpdf(string[] clauses)
    {
        _wtpdf ??= WtpdfScan.Check(_document, _cancellationToken);
        foreach (var finding in _wtpdf.Findings)
        {
            if (finding.Profile <= _level && Array.IndexOf(clauses, finding.Clause) >= 0)
            {
                return true;
            }
        }

        return false;
    }
}
