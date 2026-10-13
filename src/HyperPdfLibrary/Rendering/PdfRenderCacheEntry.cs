// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Rendering;

/// <summary>A cache shared by live renderers with the same image and drawing settings.</summary>
internal sealed class PdfRenderCacheEntry
{
    /// <summary>Initializes a new instance of the <see cref="PdfRenderCacheEntry"/> class.</summary>
    /// <param name="key">The settings that identify the cache.</param>
    /// <param name="cache">The cache owned by the document while renderers use it.</param>
    /// <param name="usesOutputIntent">Whether its content uses the document's output intent.</param>
    internal PdfRenderCacheEntry(in PdfRenderCacheKey key, PdfRenderCache cache, bool usesOutputIntent)
    {
        Key = key;
        Cache = cache;
        UsesOutputIntent = usesOutputIntent;
        Users = 1;
    }

    /// <summary>Gets the settings that identify the cache.</summary>
    internal PdfRenderCacheKey Key { get; }

    /// <summary>Gets the reusable render cache.</summary>
    internal PdfRenderCache Cache { get; }

    /// <summary>Gets a value indicating whether this entry uses output-intent colours.</summary>
    internal bool UsesOutputIntent { get; }

    /// <summary>Gets or sets the number of live renderers using this cache, guarded by the document's renderer lock.</summary>
    internal int Users { get; set; }
}
