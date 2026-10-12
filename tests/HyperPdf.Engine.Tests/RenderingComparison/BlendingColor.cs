// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Engine.Tests.RenderingComparison;

/// <summary>Represents ideal normalized RGB components before device raster quantization.</summary>
/// <param name="Red">The red component.</param>
/// <param name="Green">The green component.</param>
/// <param name="Blue">The blue component.</param>
internal readonly record struct BlendingColor(double Red, double Green, double Blue);
