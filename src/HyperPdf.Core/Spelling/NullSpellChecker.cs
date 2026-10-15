// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdf.Core.Spelling;

/// <summary>A spell checker with no dictionary, which finds every word correct.</summary>
[DebuggerDisplay("NullSpellChecker")]
public sealed class NullSpellChecker : ISpellChecker
{
    /// <summary>Gets the shared instance.</summary>
    public static NullSpellChecker Instance { get; } = new();

    /// <inheritdoc/>
    public bool IsAvailable => false;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsCorrect(ReadOnlySpan<char> word) => true;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Suggest(string word, List<string> suggestions)
    {
    }
}
