// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Reading;

/// <summary>A run of characters in a page's reading text.</summary>
/// <param name="Start">The first character, or -1 for none.</param>
/// <param name="Length">The number of characters.</param>
[DebuggerDisplay("TextRange: {Start}+{Length}")]
public readonly record struct TextRange(int Start, int Length)
{
    /// <summary>Gets the empty range.</summary>
    public static TextRange None { get; } = new(-1, 0);

    /// <summary>Gets a value indicating whether the range holds no characters.</summary>
    public bool IsEmpty => Start < 0 || Length <= 0;

    /// <summary>Gets the character after the range.</summary>
    public int End => Start + Length;
}
