// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Licences;

/// <summary>A run of characters inside the notices text.</summary>
/// <param name="Start">The index of the first character.</param>
/// <param name="Length">The number of characters.</param>
internal readonly record struct TextRange(int Start, int Length)
{
    /// <summary>Gets the characters of this range as a string.</summary>
    /// <param name="source">The text the range points into.</param>
    /// <returns>The characters; empty for an empty range.</returns>
    internal string Read(string source) => Length == 0 ? string.Empty : source.Substring(Start, Length);
}
