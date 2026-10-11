// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace PdfViewerLite.App.Rendering;

/// <summary>Remembers a failed compositor GPU path so later tiles render on the worker.</summary>
internal sealed class GpuAvailability
{
    /// <summary>One after the graphics path failed in this render hub.</summary>
    private int _unavailable;

    /// <summary>Gets whether later tiles should prepare software pixels.</summary>
    internal bool IsUnavailable => Volatile.Read(ref _unavailable) != 0;

    /// <summary>Routes subsequent tile preparation to the software path.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void MarkUnavailable() => Volatile.Write(ref _unavailable, 1);
}
