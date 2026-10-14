// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jbig2.Encoding;

/// <summary>What a page is coded with.</summary>
/// <param name="Size">The page size.</param>
/// <param name="Options">The encoder options.</param>
/// <param name="IsLossless">Whether the page is reproduced exactly.</param>
/// <param name="Refine">Whether near copies of a symbol are coded as refinements of it.</param>
internal readonly record struct Jbig2PageSettings(Jbig2Size Size, Jbig2EncodeOptions Options, bool IsLossless, bool Refine);
