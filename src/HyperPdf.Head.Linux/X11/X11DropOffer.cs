// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Head.Linux.X11;

/// <summary>The drag that is over the window now, as far as the drag and drop protocol has told the window about it.</summary>
/// <param name="Source">The window the drag came from, or 0 when no drag is over the window.</param>
/// <param name="Accepts">Whether the drag offers files, which are the only thing the window takes.</param>
[DebuggerDisplay("X11DropOffer: from {Source}, accepts {Accepts}")]
internal readonly record struct X11DropOffer(long Source, bool Accepts)
{
    /// <summary>Gets the state while no drag is over the window.</summary>
    internal static X11DropOffer None => default;
}
