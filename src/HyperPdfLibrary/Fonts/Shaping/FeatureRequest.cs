// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Fonts.Shaping;

/// <summary>A feature to apply in a stage of a shaping plan.</summary>
/// <param name="Tag">The feature tag.</param>
/// <param name="Mask">The glyph mask bit that says a glyph takes the feature; <see cref="ShapeMasks.Global"/> for every glyph.</param>
/// <param name="Value">The feature value, one for on.</param>
[DebuggerDisplay("FeatureRequest: {Tag}")]
internal readonly record struct FeatureRequest(uint Tag, uint Mask, int Value)
{
    /// <summary>Makes a request for a feature every glyph takes.</summary>
    /// <param name="tag">The feature tag.</param>
    /// <returns>The request.</returns>
    internal static FeatureRequest Everywhere(uint tag) => new(tag, ShapeMasks.Global, 1);
}
