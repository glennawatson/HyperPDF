// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Media.U3d;

/// <summary>The most a U3D decode will read, so that a hostile file cannot make it use a lot of memory or time.</summary>
/// <param name="MaxBlocks">The most blocks read, counting those inside modifier chains.</param>
/// <param name="MaxPositions">The most positions one mesh or surface may hold.</param>
/// <param name="MaxFaces">The most faces one mesh may hold.</param>
/// <param name="MaxNodes">The most scene nodes read.</param>
/// <param name="MaxControlPoints">The most control points one free-form surface may hold.</param>
/// <param name="MaxTexturePixels">The most pixels one texture may hold.</param>
/// <param name="MaxInstances">The most times a model is drawn when a frame is made, counting every path through the scene tree.</param>
/// <param name="MaxBytes">The most bytes of U3D data a PDF's 3D stream may hold before it is not decoded.</param>
[DebuggerDisplay("U3dLimits: {MaxPositions} positions, {MaxFaces} faces")]
public sealed record U3dLimits(int MaxBlocks, int MaxPositions, int MaxFaces, int MaxNodes, int MaxControlPoints, int MaxTexturePixels, int MaxInstances, long MaxBytes)
{
    /// <summary>The most blocks read by default.</summary>
    private const int DefaultBlocks = 200_000;

    /// <summary>The most positions one mesh may hold by default.</summary>
    private const int DefaultPositions = 2_000_000;

    /// <summary>The most faces one mesh may hold by default.</summary>
    private const int DefaultFaces = 4_000_000;

    /// <summary>The most scene nodes read by default.</summary>
    private const int DefaultNodes = 100_000;

    /// <summary>The most control points one surface may hold by default.</summary>
    private const int DefaultControlPoints = 250_000;

    /// <summary>The most pixels one texture may hold by default: 8192 by 8192.</summary>
    private const int DefaultTexturePixels = 67_108_864;

    /// <summary>The most model instances drawn by default.</summary>
    private const int DefaultInstances = 100_000;

    /// <summary>The most bytes of U3D data decoded by default: 128 MiB.</summary>
    private const long DefaultBytes = 128L * 1024 * 1024;

    /// <summary>Gets the limits a viewer uses.</summary>
    public static U3dLimits Default { get; } = new(DefaultBlocks, DefaultPositions, DefaultFaces, DefaultNodes, DefaultControlPoints, DefaultTexturePixels, DefaultInstances, DefaultBytes);
}
