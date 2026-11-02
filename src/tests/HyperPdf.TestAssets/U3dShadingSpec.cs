// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.TestAssets;

/// <summary>A shading description of a test mesh: whether corners have a diffuse colour, and the dimensions of each texture layer.</summary>
/// <param name="DiffuseColors"><see langword="true"/> when corners carry a diffuse colour index.</param>
/// <param name="LayerDimensions">The dimensions of each texture coordinate layer.</param>
public sealed record U3dShadingSpec(bool DiffuseColors, uint[] LayerDimensions);
