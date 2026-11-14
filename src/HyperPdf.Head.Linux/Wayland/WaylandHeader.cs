// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Head.Linux.Wayland;

/// <summary>The eight byte head of every Wayland message.</summary>
/// <param name="ObjectId">The object the message is for.</param>
/// <param name="Opcode">The request or event number on that object.</param>
/// <param name="Size">The whole message size in bytes, head included.</param>
[DebuggerDisplay("WaylandHeader object {ObjectId} opcode {Opcode} size {Size}")]
internal readonly record struct WaylandHeader(uint ObjectId, int Opcode, int Size)
{
    /// <summary>The size of the head in bytes.</summary>
    internal const int Length = 8;
}
