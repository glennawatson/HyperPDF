// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.PageObjects;

/// <summary>One link of the chain of clipping paths; saved graphics states share the links, so <c>q</c> copies nothing.</summary>
/// <param name="Parent">The clip in force before this one, or <see langword="null"/>.</param>
/// <param name="Path">The clipping path.</param>
/// <param name="Bounds">The bounds of this clip and every clip before it, in user space.</param>
/// <param name="Source">Where the operators that made the clip are in the content, when they are a path that was only a clip (<c>W n</c>); empty otherwise.</param>
[DebuggerDisplay("ClipNode: {Bounds}")]
internal sealed record ClipNode(ClipNode? Parent, PdfClipPath Path, PdfRectangle Bounds, ByteRange Source)
{
    /// <summary>Copies the chain into a list, outermost clip first.</summary>
    /// <param name="node">The innermost clip, or <see langword="null"/>.</param>
    /// <returns>The clipping paths.</returns>
    internal static PdfClipPath[] ToArray(ClipNode? node)
    {
        var count = 0;
        for (var walk = node; walk is not null; walk = walk.Parent)
        {
            count++;
        }

        var paths = new PdfClipPath[count];
        for (var walk = node; walk is not null; walk = walk.Parent)
        {
            count--;
            paths[count] = walk.Path;
        }

        return paths;
    }
}
