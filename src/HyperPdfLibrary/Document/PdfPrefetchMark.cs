// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Document;

/// <summary>
/// Remembers the cache generation at which the objects behind a dictionary were last loaded ahead, so a repeat call can
/// skip the walk while nothing has been dropped from the cache since. Each kind of walk is remembered apart, because a walk
/// for one kind does not load what another kind follows.
/// </summary>
[DebuggerDisplay("PdfPrefetchMark")]
internal sealed class PdfPrefetchMark
{
    /// <summary>The value of a generation before its first load.</summary>
    private const long NotLoaded = -1L;

    /// <summary>The generation a <see cref="PdfPrefetchKind.Page"/> walk loaded at.</summary>
    private long _page = NotLoaded;

    /// <summary>The generation a <see cref="PdfPrefetchKind.PageTree"/> walk loaded at.</summary>
    private long _pageTree = NotLoaded;

    /// <summary>The generation a <see cref="PdfPrefetchKind.Outline"/> walk loaded at.</summary>
    private long _outline = NotLoaded;

    /// <summary>The generation a <see cref="PdfPrefetchKind.Separation"/> walk loaded at.</summary>
    private long _separation = NotLoaded;

    /// <summary>Determines whether the objects a kind of walk follows were loaded at a generation.</summary>
    /// <param name="kind">The kind of walk.</param>
    /// <param name="generation">The cache generation now.</param>
    /// <returns><see langword="true"/> when they were loaded at exactly this generation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool IsLoadedAt(PdfPrefetchKind kind, long generation) => Volatile.Read(ref Slot(kind)) == generation;

    /// <summary>Records that the objects a kind of walk follows were loaded at a generation.</summary>
    /// <param name="kind">The kind of walk.</param>
    /// <param name="generation">The generation the load started at.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void SetLoadedAt(PdfPrefetchKind kind, long generation) => Volatile.Write(ref Slot(kind), generation);

    /// <summary>Finds the field that remembers a kind of walk.</summary>
    /// <param name="kind">The kind of walk.</param>
    /// <returns>A reference to the field.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ref long Slot(PdfPrefetchKind kind)
    {
        switch (kind)
        {
            case PdfPrefetchKind.Page:
                {
                    return ref _page;
                }

            case PdfPrefetchKind.PageTree:
                {
                    return ref _pageTree;
                }

            case PdfPrefetchKind.Outline:
                {
                    return ref _outline;
                }

            default:
                {
                    return ref _separation;
                }
        }
    }
}
