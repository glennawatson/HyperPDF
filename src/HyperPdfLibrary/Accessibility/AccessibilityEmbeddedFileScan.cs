// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Accessibility;

/// <summary>Checks embedded-file filename entries required by ISO 14289-1 clause 7.11.</summary>
internal static class AccessibilityEmbeddedFileScan
{
    /// <summary>The entries in a name-tree key-value pair.</summary>
    internal const int PairLength = 2;

    /// <summary>The maximum node visits in a damaged or repeatedly shared name tree.</summary>
    internal const int MaxNodeVisits = 1_000_000;

    /// <summary>Checks named embedded files without requiring the recommended Desc entry.</summary>
    /// <param name="catalog">The document catalog.</param>
    /// <param name="part">The identification part.</param>
    /// <param name="findings">Receives findings without changing source values.</param>
    /// <param name="cancellationToken">Cancels name-tree traversal.</param>
    internal static void Run(PdfDictionary catalog, int? part, AccessibilityFindings findings, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (part != PdfUaClaim.Part1
            || catalog.GetDictionary(KnownName.Names)?.GetDictionary(KnownName.EmbeddedFiles) is not { } root)
        {
            return;
        }

        if (root.GetRaw(KnownName.Kids).IsNull)
        {
            CheckNames(root.GetArray(KnownName.Names), findings, cancellationToken);
            return;
        }

        var ancestors = ArrayPool<PdfDictionary?>.Shared.Rent(PdfLimits.MaxPageTreeDepth + 1);
        try
        {
            var remaining = MaxNodeVisits;
            Walk(root, ancestors, 0, ref remaining, findings, cancellationToken);
        }
        finally
        {
            ArrayPool<PdfDictionary?>.Shared.Return(ancestors, true);
        }
    }

    /// <summary>Checks page attachments even when their icons are hidden or excluded from tab order.</summary>
    /// <param name="names">The owning document's name table.</param>
    /// <param name="annotation">The attachment annotation.</param>
    /// <param name="part">The identification part.</param>
    /// <param name="findings">Receives findings.</param>
    /// <param name="pageIndex">The containing page.</param>
    internal static void CheckAnnotation(PdfNameTable names, PdfDictionary annotation, int? part, AccessibilityFindings findings, int pageIndex)
    {
        if (part == PdfUaClaim.Part1 && annotation.IsName(KnownName.Subtype, KnownName.FileAttachment))
        {
            CheckSpecification(annotation.GetDictionary(names.Intern("FS"u8)), findings, pageIndex);
        }
    }

    /// <summary>Detects an ancestor by identity without allocating a visited set.</summary>
    /// <param name="node">The node about to be visited.</param>
    /// <param name="ancestors">The current traversal path.</param>
    /// <returns>True when following the node would revisit an ancestor.</returns>
    internal static bool IsAncestor(PdfDictionary node, ReadOnlySpan<PdfDictionary?> ancestors)
    {
        foreach (var ancestor in ancestors)
        {
            if (ReferenceEquals(node, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Traverses child nodes with a bounded path and visit budget.</summary>
    /// <param name="node">The current node.</param>
    /// <param name="ancestors">The leased traversal path.</param>
    /// <param name="depth">The current depth.</param>
    /// <param name="remaining">The remaining node visits.</param>
    /// <param name="findings">Receives findings.</param>
    /// <param name="cancellationToken">Cancels traversal.</param>
    private static void Walk(PdfDictionary node, Span<PdfDictionary?> ancestors, int depth, ref int remaining, AccessibilityFindings findings, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (depth > PdfLimits.MaxPageTreeDepth || remaining <= 0 || IsAncestor(node, ancestors[..depth]))
        {
            return;
        }

        remaining--;
        ancestors[depth] = node;
        CheckNames(node.GetArray(KnownName.Names), findings, cancellationToken);
        var children = node.GetArray(KnownName.Kids);
        for (var index = 0; children is not null && index < children.Count && remaining > 0; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (children.GetDictionary(index) is { } child)
            {
                Walk(child, ancestors, depth + 1, ref remaining, findings, cancellationToken);
            }
        }

        ancestors[depth] = null;
    }

    /// <summary>Checks only complete name-tree pairs, retaining readable entries in damaged arrays.</summary>
    /// <param name="names">The leaf's names array.</param>
    /// <param name="findings">Receives document-level findings.</param>
    /// <param name="cancellationToken">Cancels traversal.</param>
    private static void CheckNames(PdfArray? names, AccessibilityFindings findings, CancellationToken cancellationToken)
    {
        for (var index = 1; names is not null && index < names.Count; index += PairLength)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CheckSpecification(names.GetDictionary(index), findings, -1);
        }
    }

    /// <summary>Checks the required keys and their standard string types, without imposing a nonempty-name rule.</summary>
    /// <param name="specification">The file specification, when readable.</param>
    /// <param name="findings">Receives findings.</param>
    /// <param name="pageIndex">The containing page, or -1 for a named attachment.</param>
    private static void CheckSpecification(PdfDictionary? specification, AccessibilityFindings findings, int pageIndex)
    {
        if (specification is not null
            && (specification.Get(KnownName.F).Kind != PdfKind.String || specification.Get(KnownName.UF).Kind != PdfKind.String))
        {
            AccessibilityFindingCollection.Add(findings, PdfAccessibilityCode.EmbeddedFileFilenameInvalid, pageIndex);
        }
    }
}
