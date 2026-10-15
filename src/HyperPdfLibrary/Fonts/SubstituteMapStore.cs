// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Fonts;

/// <summary>
/// Keeps the code maps built for one stand-in face, so fonts that share a base encoding and need no /Differences or
/// /ToUnicode lookups copy a finished map instead of resolving 256 glyph names again. Stored maps are never handed out;
/// each caller gets its own copy to change.
/// </summary>
[DebuggerDisplay("SubstituteMapStore")]
internal sealed class SubstituteMapStore
{
    /// <summary>Guards the maps.</summary>
    private Lock? _gate;

    /// <summary>The stored maps.</summary>
    private Dictionary<SubstituteMapKey, SimpleGlyphMap>? _maps;

    /// <summary>Copies a stored map.</summary>
    /// <param name="key">What decides the map.</param>
    /// <param name="copy">Receives a copy the caller owns.</param>
    /// <returns><see langword="true"/> when a map was stored for the key.</returns>
    internal bool TryCopy(in SubstituteMapKey key, out SimpleGlyphMap copy)
    {
        lock (GetGate())
        {
            if (_maps is { } maps && maps.TryGetValue(key, out var stored))
            {
                copy = stored.Copy();
                return true;
            }
        }

        copy = null!;
        return false;
    }

    /// <summary>Stores a copy of a map unless one is already stored for the key.</summary>
    /// <param name="key">What decides the map.</param>
    /// <param name="map">The map; the store keeps its own copy.</param>
    internal void Add(in SubstituteMapKey key, SimpleGlyphMap map)
    {
        lock (GetGate())
        {
            if (_maps?.ContainsKey(key) == true)
            {
                return;
            }

            (_maps ??= []).Add(key, map.Copy());
        }
    }

    /// <summary>Gets the lock only when a code map is requested.</summary>
    /// <returns>The map lock.</returns>
    private Lock GetGate()
    {
        if (Volatile.Read(ref _gate) is { } existing)
        {
            return existing;
        }

        var created = new Lock();
        return Interlocked.CompareExchange(ref _gate, created, null) ?? created;
    }
}
