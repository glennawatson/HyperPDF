// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Media;

/// <summary>A 3D view: camera, projection and display settings.</summary>
/// <param name="ExternalName">The name shown to people (<c>/XN</c>), or null.</param>
/// <param name="InternalName">The name inside the 3D data (<c>/IN</c>), or null.</param>
/// <param name="MatrixMode">How the camera is given: M (a matrix) or U3D (a view node), or null.</param>
/// <param name="CameraToWorld">The twelve numbers of the camera-to-world matrix (<c>/C2W</c>), or an empty array.</param>
/// <param name="CameraDistance">The distance to the target (<c>/CO</c>).</param>
/// <param name="Projection">The projection type: P (perspective) or O (orthographic), or null.</param>
/// <param name="RenderMode">The render mode subtype (for example Solid or Wireframe), or null.</param>
/// <param name="Lighting">The lighting scheme subtype (for example White or Day), or null.</param>
/// <param name="Background">The background colour components (<c>/BG /C</c>), or an empty array.</param>
/// <param name="CrossSectionCount">The number of cross sections.</param>
/// <param name="NodeCount">The number of node overrides (<c>/NA</c>).</param>
[DebuggerDisplay("Pdf3DView: {ExternalName}")]
public sealed record Pdf3DView(
    string? ExternalName,
    string? InternalName,
    string? MatrixMode,
    double[] CameraToWorld,
    double CameraDistance,
    string? Projection,
    string? RenderMode,
    string? Lighting,
    double[] Background,
    int CrossSectionCount,
    int NodeCount);
