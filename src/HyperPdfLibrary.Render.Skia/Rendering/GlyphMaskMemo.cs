// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Rendering;

/// <summary>
/// A small lookup that one recording device checks before the shared glyph masks, so a glyph it has already drawn costs a hash
/// and a compare rather than the shared lock. It is owned by one device and used from one thread. Masks never change, so an
/// entry stays right until the shared cache starts again, and then the memo does too.
/// </summary>
internal sealed class GlyphMaskMemo
{
    /// <summary>The number of slots: a power of two, big enough for the glyphs of one page of text.</summary>
    private const int Slots = 128;

    /// <summary>The mask that turns a hash into a slot.</summary>
    private const int SlotMask = Slots - 1;

    /// <summary>The slots, made when the first mask is kept.</summary>
    private Slot[]? _slots;

    /// <summary>The generation of the shared cache the slots were filled from.</summary>
    private int _generation;

    /// <summary>Finds a mask, checking the memo first and then the shared cache.</summary>
    /// <param name="shared">The masks every device shares.</param>
    /// <param name="key">The glyph, size and position.</param>
    /// <param name="mask">Receives the mask.</param>
    /// <returns><see langword="true"/> when the mask was made before.</returns>
    internal bool TryGet(SkiaGlyphMasks shared, in GlyphMaskKey key, out GlyphMask? mask)
    {
        var slots = _slots;
        if (slots is not null && _generation == shared.Generation)
        {
            ref readonly var slot = ref slots[key.GetHashCode() & SlotMask];
            if (slot.Mask is not null && slot.Key.Equals(key))
            {
                mask = slot.Mask;
                return true;
            }
        }

        if (!shared.TryGet(key, out mask))
        {
            return false;
        }

        Remember(shared, key, mask);
        return true;
    }

    /// <summary>Keeps a new mask in the shared cache and in the memo.</summary>
    /// <param name="shared">The masks every device shares.</param>
    /// <param name="key">The glyph, size and position.</param>
    /// <param name="created">The mask that was made.</param>
    /// <returns>The mask the shared cache holds, which another thread may have made first.</returns>
    internal GlyphMask Add(SkiaGlyphMasks shared, in GlyphMaskKey key, GlyphMask created)
    {
        var kept = shared.Add(key, created);
        Remember(shared, key, kept);
        return kept;
    }

    /// <summary>Puts a mask in its slot, starting the memo again first when the shared cache has.</summary>
    /// <param name="shared">The masks every device shares.</param>
    /// <param name="key">The glyph, size and position.</param>
    /// <param name="mask">The mask.</param>
    private void Remember(SkiaGlyphMasks shared, in GlyphMaskKey key, GlyphMask? mask)
    {
        if (mask is null)
        {
            return;
        }

        var slots = _slots ??= new Slot[Slots];
        var generation = shared.Generation;
        if (_generation != generation)
        {
            Array.Clear(slots);
            _generation = generation;
        }

        slots[key.GetHashCode() & SlotMask] = new(key, mask);
    }

    /// <summary>One remembered mask.</summary>
    /// <param name="Key">What the mask is for.</param>
    /// <param name="Mask">The mask; <see langword="null"/> in an empty slot.</param>
    private readonly record struct Slot(GlyphMaskKey Key, GlyphMask? Mask);
}
