// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.IO;

namespace HyperPdfLibrary.Forms;

/// <summary>The underlines and strike-throughs recorded to draw after the text, in a pooled array.</summary>
[DebuggerDisplay("RichRules: {Count} rules")]
internal ref struct RichRules
{
    /// <summary>The recorded rules; a pooled array.</summary>
    private RichRule[]? _rules;

    /// <summary>Gets the number of rules.</summary>
    internal int Count { get; private set; }

    /// <summary>Gets a rule.</summary>
    /// <param name="index">The rule's place.</param>
    /// <returns>The rule.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly RichRule Get(int index) => _rules![index];

    /// <summary>Records a rule.</summary>
    /// <param name="rule">The rule.</param>
    internal void Add(RichRule rule)
    {
        PooledArrays.EnsureRoom(ref _rules, Count, clear: false)[Count] = rule;
        Count++;
    }

    /// <summary>Gives the array back.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Dispose()
    {
        PooledArrays.Return(ref _rules, Count, clear: false);
        Count = 0;
    }
}
