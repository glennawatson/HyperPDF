// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Accessibility;

/// <summary>Loads indirect definitions before accessibility checks parse them synchronously.</summary>
internal static class AccessibilityReferencePrefetch
{
    /// <summary>Follows reference chains using the core reader's limit and explicit cancellation.</summary>
    /// <param name="objects">The owning store.</param>
    /// <param name="value">The direct value or reference chain.</param>
    /// <param name="cancellationToken">Cancels I/O and each chain step.</param>
    /// <returns>The resolved value, or null when the chain exceeds the reader's limit.</returns>
    internal static async ValueTask<PdfValue> ResolveAsync(PdfObjectStore objects, PdfValue value, CancellationToken cancellationToken)
    {
        for (var step = 0; value.IsReference && step < PdfLimits.MaxReferenceChain; step++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var id = value.AsReference();
            if (StoreExtents.TryGetExtent(objects, id.Number, out var offset, out var length))
            {
                await objects.Source.PrefetchAsync(offset, length, cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            value = StoreReading.GetObject(objects, id);
        }

        return value.IsReference ? default : value;
    }
}
