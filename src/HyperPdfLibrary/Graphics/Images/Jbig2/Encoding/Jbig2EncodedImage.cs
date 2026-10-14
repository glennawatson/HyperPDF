// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Graphics.Images.Jbig2.Encoding;

/// <summary>A JBIG2 image coded for a PDF stream.</summary>
/// <param name="Data">The image stream's data: the page's segments, with no file header.</param>
/// <param name="Globals">The /JBIG2Globals stream's data, or <see langword="null"/> when the image has none.</param>
/// <param name="Mode">The coding used; for <see cref="Jbig2EncodeMode.Smallest"/> this is the one it picked.</param>
/// <param name="IsLossless">Whether decoding gives back the input exactly.</param>
[DebuggerDisplay("Jbig2EncodedImage: {Mode}, {Data.Length} bytes")]
public sealed record Jbig2EncodedImage(byte[] Data, byte[]? Globals, Jbig2EncodeMode Mode, bool IsLossless);
