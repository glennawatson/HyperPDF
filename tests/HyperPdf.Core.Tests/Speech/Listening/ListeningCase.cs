// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Tests.Speech.Listening;

/// <summary>One written phrase and the words it must, and must not, be read as.</summary>
/// <param name="Category">What it exercises, such as money or dates.</param>
/// <param name="Text">The text as written.</param>
/// <param name="British">Whether it is read with a British voice.</param>
/// <param name="Expect">Phrases the spoken text must contain.</param>
/// <param name="Forbid">Phrases the spoken text must not contain.</param>
[DebuggerDisplay("ListeningCase: {Category}: {Text}")]
public sealed record ListeningCase(string Category, string Text, bool British, IReadOnlyList<string> Expect, IReadOnlyList<string>? Forbid)
{
    /// <inheritdoc/>
    public override string ToString() => $"{Category}: {Text}{(British ? " (British)" : string.Empty)}";
}
