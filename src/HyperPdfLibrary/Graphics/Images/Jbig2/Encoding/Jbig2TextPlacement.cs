// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jbig2.Encoding;

/// <summary>Where and how a text region draws its symbols.</summary>
/// <param name="Region">The region's size and place on the page.</param>
/// <param name="SymbolCount">The number of symbols in the dictionaries the region refers to, together.</param>
/// <param name="IsLossless">Whether the instances reproduce the page exactly.</param>
/// <param name="Components">The components that refined instances take their bitmaps from; the default value when no instance is refined.</param>
/// <param name="Symbols">The symbols that refined instances refine; the default value when no instance is refined.</param>
internal readonly record struct Jbig2TextPlacement(Jbig2RegionBox Region, int SymbolCount, bool IsLossless, Jbig2Components Components, Jbig2EncoderSymbols Symbols);
