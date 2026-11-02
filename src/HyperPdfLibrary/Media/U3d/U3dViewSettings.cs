// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Media.U3d;

/// <summary>The per-instance settings of a view node: clipping, projection and view port (ECMA-363 9.5.4).</summary>
/// <param name="Attributes">The flags: 1 percent units, 2 orthographic, 4 two-point perspective, 6 one-point perspective.</param>
/// <param name="NearClip">The near clipping distance.</param>
/// <param name="FarClip">The far clipping distance.</param>
/// <param name="FieldOfView">The field of view in degrees for a three-point perspective view, or 0.</param>
/// <param name="OrthographicHeight">The height of an orthographic view, or 0.</param>
/// <param name="BackdropCount">The number of backdrops, which are not drawn.</param>
/// <param name="OverlayCount">The number of overlays, which are not drawn.</param>
[DebuggerDisplay("U3dViewSettings: fov {FieldOfView} near {NearClip} far {FarClip}")]
public sealed record U3dViewSettings(uint Attributes, float NearClip, float FarClip, float FieldOfView, float OrthographicHeight, int BackdropCount, int OverlayCount)
{
    /// <summary>The projection bits of the attributes.</summary>
    private const uint ProjectionMask = 6;

    /// <summary>The attribute value of an orthographic projection.</summary>
    private const uint Orthographic = 2;

    /// <summary>Gets a value indicating whether the view is orthographic.</summary>
    public bool IsOrthographic => (Attributes & ProjectionMask) == Orthographic;
}
