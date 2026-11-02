// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;

namespace HyperPdfLibrary.Media.U3d;

/// <summary>A view resource: the part of the scene a view draws, and its fog.</summary>
/// <param name="Name">The resource's name.</param>
/// <param name="RootNode">The node the first pass draws from; empty for the whole scene.</param>
/// <param name="FogEnabled">Whether the first pass draws fog.</param>
/// <param name="FogColor">The fog colour with its alpha.</param>
/// <param name="FogNear">Where linear fog starts.</param>
/// <param name="FogFar">Where fog is at its densest.</param>
[DebuggerDisplay("U3dViewResource: {Name}")]
public sealed record U3dViewResource(string Name, string RootNode, bool FogEnabled, Vector4 FogColor, float FogNear, float FogFar);
