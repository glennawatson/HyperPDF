// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Xfa;

/// <summary>One XFA packet, as bytes. The library does not interpret it.</summary>
/// <param name="Name">The packet name, for example template, datasets, config, localeSet, connectionSet, form, xdc or xfdf. A single-stream XFA is one packet named xdp.</param>
/// <param name="Data">The decoded bytes (usually XML).</param>
[DebuggerDisplay("PdfXfaPacket: {Name} {Data.Length} bytes")]
public sealed record PdfXfaPacket(string Name, byte[] Data);
