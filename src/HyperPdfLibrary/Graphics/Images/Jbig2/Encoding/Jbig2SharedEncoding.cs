// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Graphics.Images.Jbig2.Encoding;

/// <summary>Several images coded with one shared symbol dictionary.</summary>
/// <param name="Globals">The /JBIG2Globals data every page needs; empty when no page had symbols to share.</param>
/// <param name="Pages">Each image stream's data, in the order the images were given.</param>
/// <param name="Mode">The symbol coding used.</param>
/// <param name="IsLossless">Whether decoding gives back every image exactly.</param>
[DebuggerDisplay("Jbig2SharedEncoding: {Pages.Count} pages, {Globals.Length} globals bytes")]
public sealed record Jbig2SharedEncoding(byte[] Globals, IReadOnlyList<byte[]> Pages, Jbig2EncodeMode Mode, bool IsLossless);
