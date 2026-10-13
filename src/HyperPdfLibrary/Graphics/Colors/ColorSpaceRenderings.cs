// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Colors;

/// <summary>
/// Keeps the variants a colour space or shading makes for each ICC rendering. The slot array is made on the first request
/// for a rendering that converts differently, and each variant on its first request; both are shared by every thread.
/// </summary>
internal static class ColorSpaceRenderings
{
    /// <summary>Gets the variant for a rendering, creating it on first request.</summary>
    /// <typeparam name="TVariant">The type of the variant.</typeparam>
    /// <typeparam name="TOwner">The type of the object the variant is made from.</typeparam>
    /// <typeparam name="TInner">The type of the conversion the variant wraps.</typeparam>
    /// <param name="variants">The owner's slot array, or <see langword="null"/> before the first variant.</param>
    /// <param name="rendering">The rendering.</param>
    /// <param name="owner">The object the variant is made from.</param>
    /// <param name="inner">The conversion the variant uses for the rendering.</param>
    /// <param name="create">Makes the variant from the owner and the conversion.</param>
    /// <returns>The variant; the same instance on every call for the rendering.</returns>
    internal static TVariant GetOrCreate<TVariant, TOwner, TInner>(
        ref TVariant?[]? variants,
        IccRendering rendering,
        TOwner owner,
        TInner inner,
        Func<TOwner, TInner, TVariant> create)
        where TVariant : class
    {
        var slots = Volatile.Read(ref variants);
        if (slots is null)
        {
            var made = new TVariant?[IccRendering.Count];
            slots = Interlocked.CompareExchange(ref variants, made, null) ?? made;
        }

        ref var slot = ref slots[rendering.Slot];
        var existing = Volatile.Read(ref slot);
        if (existing is not null)
        {
            return existing;
        }

        var created = create(owner, inner);
        return Interlocked.CompareExchange(ref slot, created, null) ?? created;
    }
}
