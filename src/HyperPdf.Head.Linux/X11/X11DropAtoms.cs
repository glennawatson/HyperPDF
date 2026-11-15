// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Head.Linux.X11;

/// <summary>The atoms the drag and drop protocol (XDND) names its messages and values with.</summary>
/// <param name="Aware">The property that tells a program the window takes drops.</param>
/// <param name="Enter">The message that says a drag came over the window.</param>
/// <param name="Position">The message that says where the drag is.</param>
/// <param name="Status">The message that says whether the window takes the drop at that place.</param>
/// <param name="Leave">The message that says the drag left the window.</param>
/// <param name="Drop">The message that says the drag was let go over the window.</param>
/// <param name="Finished">The message that says the window is done with the drop.</param>
/// <param name="TypeList">The property that lists every type the drag offers, for a drag that offers more than three.</param>
/// <param name="Selection">The selection the dragged data is asked for from.</param>
/// <param name="CopyAction">The action that copies the data.</param>
/// <param name="UriList">The type that lists files as addresses.</param>
/// <param name="Data">The property the dragged data is received in.</param>
[DebuggerDisplay("X11DropAtoms")]
internal readonly record struct X11DropAtoms(
    long Aware,
    long Enter,
    long Position,
    long Status,
    long Leave,
    long Drop,
    long Finished,
    long TypeList,
    long Selection,
    long CopyAction,
    long UriList,
    long Data);
