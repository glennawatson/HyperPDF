// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Composition;

/// <summary>How far a line is pushed in from each edge of the text area, for example by a frame that text wraps round.</summary>
/// <param name="Left">The space taken on the left in points.</param>
/// <param name="Right">The space taken on the right in points.</param>
[DebuggerDisplay("LineSpan: {Left}, {Right}")]
internal readonly record struct LineSpan(float Left, float Right)
{
    /// <summary>Gets a span that takes no space.</summary>
    internal static LineSpan None => default;
}
