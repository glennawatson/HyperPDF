// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Accessibility;

/// <summary>Prefetches name-tree and annotation file specifications before checking their filename entries.</summary>
internal static class AccessibilityEmbeddedFilePrefetch
{
    /// <summary>Skips in-memory sources and documents without attachment candidates.</summary>
    /// <param name="document">The document with prefetched pages.</param>
    /// <param name="cancellationToken">Cancels reads and traversal.</param>
    /// <returns>A task completing when the scanner's definitions are cached.</returns>
    internal static ValueTask RunAsync(PdfDocument document, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return document.Objects.Source.NeedsPrefetch
            && (!document.Catalog.GetRaw(KnownName.Names).IsNull || HasAnnotations(document, cancellationToken))
            ? LoadAsync(document, cancellationToken)
            : ValueTask.CompletedTask;
    }

    /// <summary>Looks for annotation arrays in the already prefetched page dictionaries.</summary>
    /// <param name="document">The document.</param>
    /// <param name="cancellationToken">Cancels page traversal.</param>
    /// <returns>True when a page may have attachment annotations.</returns>
    private static bool HasAnnotations(PdfDocument document, CancellationToken cancellationToken)
    {
        for (var index = 0; index < document.PageCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!PdfDocumentPages.GetPage(document, index).Dictionary.GetRaw(KnownName.Annots).IsNull)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Loads definitions only for the identification part checked by the filename scanner.</summary>
    /// <param name="document">The document.</param>
    /// <param name="cancellationToken">Cancels loading.</param>
    /// <returns>A task completing when required values are cached.</returns>
    private static async ValueTask LoadAsync(PdfDocument document, CancellationToken cancellationToken)
    {
        var objects = document.Objects;
        var catalog = document.Catalog;
        _ = await AccessibilityReferencePrefetch.ResolveAsync(objects, catalog.GetRaw(KnownName.Metadata), cancellationToken).ConfigureAwait(false);
        var metadata = AccessibilityMetadataReader.Read(PdfDocumentMetadata.ReadOwnerXmp(document, catalog, cancellationToken));
        if (metadata.Part != PdfUaClaim.Part1)
        {
            return;
        }

        var names = (await AccessibilityReferencePrefetch.ResolveAsync(objects, catalog.GetRaw(KnownName.Names), cancellationToken).ConfigureAwait(false)).AsDictionary();
        if (names is not null)
        {
            await LoadTreeAsync(objects, names.GetRaw(KnownName.EmbeddedFiles), cancellationToken).ConfigureAwait(false);
        }

        for (var index = 0; index < document.PageCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = PdfDocumentPages.GetPage(document, index);
            await LoadAnnotationsAsync(objects, page.Dictionary.GetRaw(KnownName.Annots), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Leases the bounded path used to stop cyclic name trees.</summary>
    /// <param name="objects">The owning store.</param>
    /// <param name="root">The tree root or reference.</param>
    /// <param name="cancellationToken">Cancels loading.</param>
    /// <returns>A task completing when filename definitions are cached.</returns>
    private static async ValueTask LoadTreeAsync(PdfObjectStore objects, PdfValue root, CancellationToken cancellationToken)
    {
        if (root.IsNull)
        {
            return;
        }

        var ancestors = ArrayPool<PdfDictionary?>.Shared.Rent(PdfLimits.MaxPageTreeDepth + 1);
        try
        {
            _ = await LoadNodeAsync(objects, root, ancestors, 0, AccessibilityEmbeddedFileScan.MaxNodeVisits, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<PdfDictionary?>.Shared.Return(ancestors, true);
        }
    }

    /// <summary>Loads each node's arrays before reading their raw entries.</summary>
    /// <param name="objects">The owning store.</param>
    /// <param name="value">The node or reference.</param>
    /// <param name="ancestors">The leased traversal path.</param>
    /// <param name="depth">The current depth.</param>
    /// <param name="remaining">The remaining node visits.</param>
    /// <param name="cancellationToken">Cancels loading.</param>
    /// <returns>The remaining visit budget.</returns>
    private static async ValueTask<int> LoadNodeAsync(PdfObjectStore objects, PdfValue value, PdfDictionary?[] ancestors, int depth, int remaining, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (depth > PdfLimits.MaxPageTreeDepth || remaining <= 0)
        {
            return remaining;
        }

        var node = (await AccessibilityReferencePrefetch.ResolveAsync(objects, value, cancellationToken).ConfigureAwait(false)).AsDictionary();
        if (node is null || AccessibilityEmbeddedFileScan.IsAncestor(node, ancestors.AsSpan(0, depth)))
        {
            return remaining;
        }

        remaining--;
        ancestors[depth] = node;
        var names = (await AccessibilityReferencePrefetch.ResolveAsync(objects, node.GetRaw(KnownName.Names), cancellationToken).ConfigureAwait(false)).AsArray();
        await LoadNamesAsync(objects, names, cancellationToken).ConfigureAwait(false);
        var children = (await AccessibilityReferencePrefetch.ResolveAsync(objects, node.GetRaw(KnownName.Kids), cancellationToken).ConfigureAwait(false)).AsArray();
        for (var index = 0; children is not null && index < children.Count && remaining > 0; index++)
        {
            remaining = await LoadNodeAsync(objects, children.GetRaw(index), ancestors, depth + 1, remaining, cancellationToken).ConfigureAwait(false);
        }

        ancestors[depth] = null;
        return remaining;
    }

    /// <summary>Loads file specifications from complete leaf pairs.</summary>
    /// <param name="objects">The owning store.</param>
    /// <param name="names">The leaf's names array.</param>
    /// <param name="cancellationToken">Cancels loading.</param>
    /// <returns>A task completing when filename entries are cached.</returns>
    private static async ValueTask LoadNamesAsync(PdfObjectStore objects, PdfArray? names, CancellationToken cancellationToken)
    {
        for (var index = 1; names is not null && index < names.Count; index += AccessibilityEmbeddedFileScan.PairLength)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await LoadSpecificationAsync(objects, names.GetRaw(index), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Loads filename entries without reading the embedded file's payload.</summary>
    /// <param name="objects">The owning store.</param>
    /// <param name="value">The file specification or reference.</param>
    /// <param name="cancellationToken">Cancels loading.</param>
    /// <returns>A task completing when both filename values are cached.</returns>
    private static async ValueTask LoadSpecificationAsync(PdfObjectStore objects, PdfValue value, CancellationToken cancellationToken)
    {
        var specification = (await AccessibilityReferencePrefetch.ResolveAsync(objects, value, cancellationToken).ConfigureAwait(false)).AsDictionary();
        if (specification is null)
        {
            return;
        }

        _ = await AccessibilityReferencePrefetch.ResolveAsync(objects, specification.GetRaw(KnownName.F), cancellationToken).ConfigureAwait(false);
        _ = await AccessibilityReferencePrefetch.ResolveAsync(objects, specification.GetRaw(KnownName.UF), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Loads attachment file specifications independently of icon visibility.</summary>
    /// <param name="objects">The owning store.</param>
    /// <param name="value">The annotation array or reference.</param>
    /// <param name="cancellationToken">Cancels loading.</param>
    /// <returns>A task completing when attachment filename entries are cached.</returns>
    private static async ValueTask LoadAnnotationsAsync(PdfObjectStore objects, PdfValue value, CancellationToken cancellationToken)
    {
        var annotations = (await AccessibilityReferencePrefetch.ResolveAsync(objects, value, cancellationToken).ConfigureAwait(false)).AsArray();
        for (var index = 0; annotations is not null && index < annotations.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var annotation = (await AccessibilityReferencePrefetch.ResolveAsync(objects, annotations.GetRaw(index), cancellationToken).ConfigureAwait(false)).AsDictionary();
            if (annotation is null)
            {
                continue;
            }

            _ = await AccessibilityReferencePrefetch.ResolveAsync(objects, annotation.GetRaw(KnownName.Subtype), cancellationToken).ConfigureAwait(false);
            if (annotation.IsName(KnownName.Subtype, KnownName.FileAttachment))
            {
                await LoadSpecificationAsync(objects, annotation.GetRaw(objects.Names.Intern("FS"u8)), cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
