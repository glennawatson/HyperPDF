// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdf.Rendering.Skia;

/// <summary>Remembers a failed compositor GPU path so later tiles render on the worker.</summary>
[DebuggerDisplay("GpuAvailability: unavailable={IsUnavailable}")]
public sealed class GpuAvailability : IDisposable
{
    /// <summary>One after the graphics path failed in this render hub.</summary>
    private int _unavailable;

    /// <summary>Gets the page-tone resources shared by tiles in this render hub.</summary>
    internal GpuPageTone PageTone { get; } = new();

    /// <summary>Gets whether later tiles should prepare software pixels.</summary>
    internal bool IsUnavailable => Volatile.Read(ref _unavailable) != 0;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => PageTone.Dispose();

    /// <summary>Routes subsequent tile preparation to software and reports the first transition.</summary>
    /// <returns>Whether the graphics path became unavailable in this call.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool MarkUnavailable() => Interlocked.Exchange(ref _unavailable, 1) == 0;
}
