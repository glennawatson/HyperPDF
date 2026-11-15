// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Viewer.Text;

/// <summary>Where a piece of text starts.</summary>
/// <param name="X">The x of the first glyph's origin in pixels.</param>
/// <param name="Baseline">The y of the baseline in pixels, growing downwards.</param>
internal readonly record struct TextOrigin(float X, float Baseline);
