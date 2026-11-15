// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using HyperPdfLibrary.Graphics;

namespace HyperPdfLibrary.Render.Managed.Raster;

/// <summary>How the triangles of a mesh shading are drawn.</summary>
/// <param name="Ramp">The colour ramp corners look their colour up in, or <see langword="null"/> when corners carry colours.</param>
/// <param name="ToDevice">The matrix from the mesh's space to device pixels.</param>
/// <param name="Alpha">How opaque the mesh is, where 255 is fully.</param>
/// <param name="Blend">How the mesh mixes with what is under it.</param>
[DebuggerDisplay("MeshJob: alpha {Alpha}")]
internal readonly record struct MeshJob(ManagedRenderShader? Ramp, Matrix3x2 ToDevice, uint Alpha, PdfBlendMode Blend);
