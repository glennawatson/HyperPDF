// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Fonts;

/// <summary>Owns font resources reused by documents that select the same drawing services.</summary>
[DebuggerDisplay("Font resources, generation {Generation}")]
internal sealed class PdfFontResources
{
    /// <summary>The resource availability and download state, created on first use.</summary>
    private FontDataResourceState? _data;

    /// <summary>Gets the parsed bundled faces.</summary>
    internal BundledFaceState Faces
    {
        get
        {
            var cached = Volatile.Read(ref field);
            if (cached is not null)
            {
                return cached;
            }

            var created = new BundledFaceState();
            return Interlocked.CompareExchange(ref field, created, null) ?? created;
        }
    }

    /// <summary>Gets the CID Unicode tables.</summary>
    internal CidUnicodeState Unicode
    {
        get
        {
            var cached = Volatile.Read(ref field);
            if (cached is not null)
            {
                return cached;
            }

            var created = new CidUnicodeState();
            return Interlocked.CompareExchange(ref field, created, null) ?? created;
        }
    }

    /// <summary>Gets the predefined CMaps.</summary>
    internal PredefinedCMapState CMaps
    {
        get
        {
            var cached = Volatile.Read(ref field);
            if (cached is not null)
            {
                return cached;
            }

            var created = new PredefinedCMapState();
            return Interlocked.CompareExchange(ref field, created, null) ?? created;
        }
    }

    /// <summary>Gets resource availability and download state.</summary>
    internal FontDataResourceState Data
    {
        get
        {
            var cached = Volatile.Read(ref _data);
            if (cached is not null)
            {
                return cached;
            }

            var created = new FontDataResourceState();
            return Interlocked.CompareExchange(ref _data, created, null) ?? created;
        }
    }

    /// <summary>Gets the single-character text cache.</summary>
    internal GlyphNameTextState GlyphNames
    {
        get
        {
            var cached = Volatile.Read(ref field);
            if (cached is not null)
            {
                return cached;
            }

            var created = new GlyphNameTextState();
            return Interlocked.CompareExchange(ref field, created, null) ?? created;
        }
    }

    /// <summary>Gets the generation without creating resource state.</summary>
    internal int Generation => Volatile.Read(ref _data)?.Generation ?? 0;

    /// <summary>Gets resources owned by a dictionary's document, or detached resources.</summary>
    /// <param name="dictionary">The dictionary.</param>
    /// <returns>The resources.</returns>
    internal static PdfFontResources For(PdfDictionary dictionary) => dictionary.Owner?.Context?.DrawingServices.FontResources ?? new();

    /// <summary>Closes any client created while preparing a detached font.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void CloseDownloadClient() => Volatile.Read(ref _data)?.CloseDownloadClient();
}
