// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.IO;

namespace HyperPdfLibrary.Forms;

/// <summary>The fonts an appearance uses, one for each name, in a pooled array.</summary>
[DebuggerDisplay("FormFontSet: {Count} fonts")]
internal ref struct FormFontSet
{
    /// <summary>The fonts; a pooled array.</summary>
    private FormFont[]? _fonts;

    /// <summary>Gets the number of fonts.</summary>
    internal int Count { get; private set; }

    /// <summary>Gets the fonts.</summary>
    /// <returns>The fonts, in the order they were added.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly ReadOnlySpan<FormFont> AsSpan() => _fonts.AsSpan(0, Count);

    /// <summary>Finds a font by its name.</summary>
    /// <param name="name">The name in the resources' <c>/Font</c> dictionary.</param>
    /// <returns>The font; <see langword="null"/> when none has that name.</returns>
    internal readonly FormFont? Find(string name)
    {
        for (var i = 0; i < Count; i++)
        {
            if (string.Equals(_fonts![i].Name, name, StringComparison.Ordinal))
            {
                return _fonts[i];
            }
        }

        return null;
    }

    /// <summary>Adds a font when no font of that name is there.</summary>
    /// <param name="font">The font.</param>
    /// <returns>The font in the set with that name.</returns>
    internal FormFont Use(FormFont font)
    {
        if (Find(font.Name) is { } existing)
        {
            return existing;
        }

        PooledArrays.EnsureRoom(ref _fonts, Count, clear: true)[Count] = font;
        Count++;
        return font;
    }

    /// <summary>Gives the array back.</summary>
    internal void Dispose()
    {
        PooledArrays.Return(ref _fonts, Count, clear: true);
        Count = 0;
    }
}
