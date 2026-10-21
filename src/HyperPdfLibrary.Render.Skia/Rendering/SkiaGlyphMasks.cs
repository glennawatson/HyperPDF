// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Rendering;

/// <summary>
/// The glyph masks of every page this backend draws, shared by all its recordings and safe to use from many threads. Small text
/// repeats the same glyphs at the same size, so a mask is made once and then placed on each page that needs it. When the
/// masks hold more memory than the limit the cache starts again; the pictures that already hold a mask keep it.
/// </summary>
[DebuggerDisplay("SkiaGlyphMasks: {Count} masks, {Bytes} bytes")]
internal sealed class SkiaGlyphMasks
{
    /// <summary>The most memory the masks hold before the cache starts again: 32 MiB.</summary>
    private const long MaxBytes = 32L * 1024 * 1024;

    /// <summary>Guards the masks and their memory count.</summary>
    private readonly Lock _gate = new();

    /// <summary>The masks.</summary>
    private readonly Dictionary<GlyphMaskKey, GlyphMask> _masks = [];

    /// <summary>The memory the masks hold.</summary>
    private long _bytes;

    /// <summary>How many times the cache has started again.</summary>
    private int _generation;

    /// <summary>Gets how many times the cache has started again, so a device memo can tell when its masks are no longer the cache's.</summary>
    internal int Generation => Volatile.Read(ref _generation);

    /// <summary>Gets the number of masks held.</summary>
    internal int Count
    {
        get
        {
            lock (_gate)
            {
                return _masks.Count;
            }
        }
    }

    /// <summary>Gets the memory the masks hold, in bytes.</summary>
    internal long Bytes
    {
        get
        {
            lock (_gate)
            {
                return _bytes;
            }
        }
    }

    /// <summary>Finds a mask.</summary>
    /// <param name="key">The glyph, size and position.</param>
    /// <param name="mask">Receives the mask.</param>
    /// <returns><see langword="true"/> when the mask was made before.</returns>
    internal bool TryGet(in GlyphMaskKey key, out GlyphMask? mask)
    {
        lock (_gate)
        {
            return _masks.TryGetValue(key, out mask);
        }
    }

    /// <summary>Keeps a mask. When another thread made the same mask first, that one is kept and returned.</summary>
    /// <param name="key">The glyph, size and position.</param>
    /// <param name="mask">The mask.</param>
    /// <returns>The mask the cache holds.</returns>
    internal GlyphMask Add(in GlyphMaskKey key, GlyphMask mask)
    {
        lock (_gate)
        {
            if (_masks.TryGetValue(key, out var existing))
            {
                return existing;
            }

            if (_bytes + mask.Bytes > MaxBytes)
            {
                // Images are not disposed here: a picture being recorded may still be about to draw one it just found.
                _masks.Clear();
                _bytes = 0;
                _ = Interlocked.Increment(ref _generation);
            }

            _masks.Add(key, mask);
            _bytes += mask.Bytes;
            return mask;
        }
    }
}
