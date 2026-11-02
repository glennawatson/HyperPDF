// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;

namespace HyperPdfLibrary.Media.U3d;

/// <summary>A scene node with the transform that places it under each of its parents.</summary>
/// <param name="Kind">The kind of node.</param>
/// <param name="Name">The node's name.</param>
/// <param name="Parents">The names of the parent nodes; an empty name is the root of the scene.</param>
/// <param name="Transforms">The transform that places the node relative to the parent of the same index.</param>
/// <param name="Resource">The name of the model, light or view resource, or empty for a group.</param>
/// <param name="Visibility">For a model node: 0 not visible, 1 front faces, 2 back faces, 3 both.</param>
/// <param name="View">The view settings of a view node, or <see langword="null"/>.</param>
[DebuggerDisplay("U3dSceneNode: {Kind} {Name}")]
public sealed record U3dSceneNode(U3dNodeKind Kind, string Name, string[] Parents, Matrix4x4[] Transforms, string Resource, uint Visibility, U3dViewSettings? View);
