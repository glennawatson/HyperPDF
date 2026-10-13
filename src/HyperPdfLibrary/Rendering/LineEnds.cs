// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Rendering;

/// <summary>The end points and line endings of a line annotation (ISO 32000-2 12.5.6.7, <c>/L</c> and <c>/LE</c>).</summary>
/// <param name="Start">The first end point.</param>
/// <param name="End">The second end point.</param>
/// <param name="StartEnding">The ending at the first end.</param>
/// <param name="EndEnding">The ending at the second end.</param>
[DebuggerDisplay("LineEnds: {Start} to {End}")]
internal readonly record struct LineEnds(Vector2 Start, Vector2 End, KnownName StartEnding, KnownName EndEnding)
{
    /// <summary>The numbers in an <c>/L</c> entry.</summary>
    private const int LineNumbers = 4;

    /// <summary>The position of the end x in <c>/L</c>.</summary>
    private const int EndX = 2;

    /// <summary>The position of the end y in <c>/L</c>.</summary>
    private const int EndY = 3;

    /// <summary>Reads <c>/L</c> and <c>/LE</c>.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="ends">Receives the ends.</param>
    /// <returns><see langword="false"/> without the four numbers of <c>/L</c>.</returns>
    internal static bool TryRead(PdfDictionary annotation, out LineEnds ends)
    {
        ends = default;
        if (annotation.GetArray(KnownName.L) is not { Count: >= LineNumbers } line)
        {
            return false;
        }

        var endings = annotation.GetArray(KnownName.LE);
        ends = new(
            new(line.GetSingle(0), line.GetSingle(1)),
            new(line.GetSingle(EndX), line.GetSingle(EndY)),
            endings?.GetName(0).ToKnownName() ?? KnownName.None,
            endings?.GetName(1).ToKnownName() ?? KnownName.None);
        return true;
    }
}
