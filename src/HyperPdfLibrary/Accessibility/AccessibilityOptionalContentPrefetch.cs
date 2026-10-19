// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Accessibility;

/// <summary>Loads indirect configuration data with async I/O before the accessibility scan.</summary>
internal static class AccessibilityOptionalContentPrefetch
{
    /// <summary>Skips sources already in memory and catalogs without optional-content properties.</summary>
    /// <param name="document">The document owning the object cache.</param>
    /// <param name="cancellationToken">Cancels reads and reference traversal.</param>
    /// <returns>A task completing when the scanner's configuration objects are cached.</returns>
    internal static ValueTask RunAsync(PdfDocument document, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return document.Objects.Source.NeedsPrefetch && !document.Catalog.GetRaw(KnownName.OCProperties).IsNull
            ? LoadAsync(document, cancellationToken)
            : ValueTask.CompletedTask;
    }

    /// <summary>Loads configuration data only for the two PDF/UA profiles the scanner checks.</summary>
    /// <param name="document">The document owning the object cache.</param>
    /// <param name="cancellationToken">Cancels loading.</param>
    /// <returns>A task completing after the required configuration data is cached.</returns>
    private static async ValueTask LoadAsync(PdfDocument document, CancellationToken cancellationToken)
    {
        var objects = document.Objects;
        var catalog = document.Catalog;
        _ = await AccessibilityReferencePrefetch.ResolveAsync(objects, catalog.GetRaw(KnownName.Metadata), cancellationToken).ConfigureAwait(false);
        var metadata = AccessibilityMetadataReader.Read(PdfDocumentMetadata.ReadOwnerXmp(document, catalog, cancellationToken));
        if (metadata.Part is not (PdfUaClaim.Part1 or PdfUaClaim.Part2))
        {
            return;
        }

        var properties = (await AccessibilityReferencePrefetch.ResolveAsync(objects, catalog.GetRaw(KnownName.OCProperties), cancellationToken).ConfigureAwait(false)).AsDictionary();
        if (properties is null)
        {
            return;
        }

        var configuration = (await AccessibilityReferencePrefetch.ResolveAsync(objects, properties.GetRaw(KnownName.D), cancellationToken).ConfigureAwait(false)).AsDictionary();
        await LoadConfigurationAsync(objects, configuration, cancellationToken).ConfigureAwait(false);
        var configurations = (await AccessibilityReferencePrefetch.ResolveAsync(objects, properties.GetRaw(KnownName.Configs), cancellationToken).ConfigureAwait(false)).AsArray();
        if (configurations is null)
        {
            return;
        }

        for (var index = 0; index < configurations.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            configuration = (await AccessibilityReferencePrefetch.ResolveAsync(objects, configurations.GetRaw(index), cancellationToken).ConfigureAwait(false)).AsDictionary();
            await LoadConfigurationAsync(objects, configuration, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Resolves the two scalar entries the scanner reads without following unrelated layer content.</summary>
    /// <param name="objects">The configuration's owning store.</param>
    /// <param name="configuration">The configuration, or null for damaged array entries.</param>
    /// <param name="cancellationToken">Cancels loading.</param>
    /// <returns>A task completing when the entries are cached.</returns>
    private static async ValueTask LoadConfigurationAsync(PdfObjectStore objects, PdfDictionary? configuration, CancellationToken cancellationToken)
    {
        if (configuration is null)
        {
            return;
        }

        _ = await AccessibilityReferencePrefetch.ResolveAsync(objects, configuration.GetRaw(KnownName.Name), cancellationToken).ConfigureAwait(false);
        _ = await AccessibilityReferencePrefetch.ResolveAsync(objects, configuration.GetRaw(KnownName.AS), cancellationToken).ConfigureAwait(false);
    }
}
