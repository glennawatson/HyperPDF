// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Accessibility;

/// <summary>A stretch of text, as offsets in UTF-16 code units.</summary>
/// <param name="Start">The offset of the first character.</param>
/// <param name="End">The offset just after the last character.</param>
[DebuggerDisplay("TextSpan: {Start}..{End}")]
public readonly record struct TextSpan(int Start, int End)
{
    /// <summary>Gets the number of characters.</summary>
    public int Length => End - Start;

    /// <summary>Gets a value indicating whether the stretch holds no characters.</summary>
    public bool IsEmpty => End <= Start;
}
