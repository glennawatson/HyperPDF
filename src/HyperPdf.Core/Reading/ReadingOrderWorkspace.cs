// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Reading;

/// <summary>Retains the working buffers used to read pages for one document or caller.</summary>
[DebuggerDisplay("ReadingOrderWorkspace: {_spare}")]
public sealed class ReadingOrderWorkspace
{
    /// <summary>The largest text buffer retained for the next page.</summary>
    private const int MaxRetainedChars = 1 << 20;

    /// <summary>The scratch offered to the next caller.</summary>
    private ReadingScratch? _spare;

    /// <summary>Takes a scratch that no other caller holds.</summary>
    /// <returns>The scratch.</returns>
    internal ReadingScratch Rent() => Interlocked.Exchange(ref _spare, null) ?? new();

    /// <summary>Returns a cleared scratch for another page when it is reasonably sized.</summary>
    /// <param name="scratch">The completed page's scratch.</param>
    internal void Return(ReadingScratch scratch)
    {
        scratch.Clear();
        if (scratch.Chars.Length <= MaxRetainedChars)
        {
            _ = Interlocked.CompareExchange(ref _spare, scratch, null);
        }
    }
}
