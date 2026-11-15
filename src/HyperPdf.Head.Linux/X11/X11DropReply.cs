// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Head.Linux.X11;

/// <summary>What a window says back to the source of a drag: whether it takes the drop, and what it will do with it.</summary>
/// <param name="Type">The atom of the message.</param>
/// <param name="Sender">The window that sends the reply.</param>
/// <param name="Flags">Whether the window takes the drop.</param>
/// <param name="Action">The atom of what the window does with the drop, or 0 for nothing.</param>
[DebuggerDisplay("X11DropReply: flags {Flags}")]
internal readonly record struct X11DropReply(long Type, long Sender, long Flags, long Action);
