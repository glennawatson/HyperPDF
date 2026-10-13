// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Drawing;

namespace HyperPdfLibrary.Rendering;

/// <summary>
/// Keeps the clip path of recently recorded page sizes. Every page recording clips to its page rectangle, and most pages of
/// a document share a size, so the immutable path is made once and shared across pages, documents and threads.
/// </summary>
internal static class PageClips
{
    /// <summary>The page sizes kept; sizes that hash to the same slot replace each other.</summary>
    private const int Slots = 8;

    /// <summary>The most recent clip in each slot.</summary>
    private static readonly PageClip?[] Recent = new PageClip?[Slots];

    /// <summary>Gets the path of a page rectangle.</summary>
    /// <param name="bounds">The page rectangle.</param>
    /// <returns>The shared path, which is never changed.</returns>
    internal static PdfPath Get(PdfRect bounds)
    {
        var slot = (int)((uint)HashCode.Combine(bounds.Right, bounds.Bottom) % Slots);
        if (Volatile.Read(ref Recent[slot]) is { } kept && kept.Bounds == bounds)
        {
            return kept.Path;
        }

        var builder = new PdfPathBuilder();
        builder.AddRect(bounds);
        var created = new PageClip(bounds, builder.Detach());
        Volatile.Write(ref Recent[slot], created);
        return created.Path;
    }

    /// <summary>A page rectangle and its clip path.</summary>
    /// <param name="Bounds">The page rectangle.</param>
    /// <param name="Path">The path that outlines it.</param>
    private sealed record PageClip(PdfRect Bounds, PdfPath Path);
}
