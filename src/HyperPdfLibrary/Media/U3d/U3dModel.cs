// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Media.U3d;

/// <summary>
/// What a U3D file holds, decoded: the scene nodes with their transforms, the triangle meshes, the materials, shaders, lights,
/// view resources and textures, and the meta data. Anything the decode could not read or does not apply is listed in
/// <see cref="Problems"/>, and the rest is kept. The file's bytes are never changed.
/// </summary>
/// <param name="Structure">The file's structure, as <see cref="U3dReader"/> reads it.</param>
/// <param name="Nodes">The scene nodes.</param>
/// <param name="Meshes">The meshes, in the order their model resources were declared.</param>
/// <param name="Materials">The materials.</param>
/// <param name="Shaders">The shaders.</param>
/// <param name="Lights">The lights.</param>
/// <param name="ViewResources">The view resources.</param>
/// <param name="Textures">The textures.</param>
/// <param name="Shadings">The shading modifiers.</param>
/// <param name="Metadata">The meta data entries, which are only recorded.</param>
/// <param name="Problems">What was not read or not applied, in plain words; empty when everything was decoded.</param>
[DebuggerDisplay("U3dModel: {Meshes.Length} meshes, {Nodes.Length} nodes")]
public sealed record U3dModel(
    U3dScene Structure,
    U3dSceneNode[] Nodes,
    U3dMesh[] Meshes,
    U3dMaterial[] Materials,
    U3dShader[] Shaders,
    U3dLight[] Lights,
    U3dViewResource[] ViewResources,
    U3dTexture[] Textures,
    U3dShadingBinding[] Shadings,
    U3dMetadataEntry[] Metadata,
    string[] Problems)
{
    /// <summary>Gets a value indicating whether everything in the file was decoded.</summary>
    public bool IsComplete => Problems.Length == 0;

    /// <summary>Gets the nodes that are views.</summary>
    /// <returns>The view nodes, in file order.</returns>
    public U3dSceneNode[] ViewNodes()
    {
        var views = new List<U3dSceneNode>();
        foreach (var node in Nodes)
        {
            if (node.Kind == U3dNodeKind.View)
            {
                views.Add(node);
            }
        }

        return [.. views];
    }
}
