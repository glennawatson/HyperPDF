// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdf.Core.Rendering;

/// <summary>
/// A consumer of rendered tiles, such as a page canvas. Requests carry the client generation at the time they were
/// made; advancing the generation drops any queued request the client did not re-issue.
/// </summary>
[DebuggerDisplay("RenderClient: Generation {Generation}")]
public sealed class RenderClient
{
    /// <summary>The current generation.</summary>
    private int _generation;

    /// <summary>Gets the current generation.</summary>
    public int Generation => Volatile.Read(ref _generation);

    /// <summary>Advances the generation, making every outstanding request stale until it is requested again.</summary>
    /// <returns>The new generation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Advance() => Interlocked.Increment(ref _generation);
}
