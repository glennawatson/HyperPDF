// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Tests.Graphics.Jpx;

/// <summary>One written packet: its SOP marker, its header and its body.</summary>
/// <param name="Start">The SOP marker segment, or empty.</param>
/// <param name="Header">The packet header bytes, with any EPH marker.</param>
/// <param name="Body">The packet body bytes.</param>
[DebuggerDisplay("JpxTestPacket: {Header.Length} + {Body.Length} bytes")]
internal sealed record JpxTestPacket(byte[] Start, byte[] Header, byte[] Body);
