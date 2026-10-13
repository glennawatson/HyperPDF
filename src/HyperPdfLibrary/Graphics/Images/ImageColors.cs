// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Graphics.Colors;

namespace HyperPdfLibrary.Graphics.Images;

/// <summary>How an image's colours convert: the output intent substitutes for device spaces and the ICC rendering.</summary>
/// <param name="Substitutes">The output-intent substitutions for device spaces, or <see langword="null"/> for the fixed conversions.</param>
/// <param name="Rendering">The rendering intent and black point choice ICC colours convert with.</param>
internal readonly record struct ImageColors(OutputIntentColors? Substitutes, IccRendering Rendering);
