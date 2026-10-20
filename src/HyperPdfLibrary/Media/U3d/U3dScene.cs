// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Media.U3d;

/// <summary>
/// What a U3D file declares: its header, its scene nodes, the size of its shapes and the resources it names. The shapes' data is
/// not decoded, and the file's bytes are never changed, so saving a PDF keeps the 3D data exactly as it was.
/// </summary>
/// <param name="MajorVersion">The major version of the format the file was written in.</param>
/// <param name="MinorVersion">The minor version.</param>
/// <param name="Profile">The profile flags: 2 for extensible, 4 for no compression, 8 for defined units.</param>
/// <param name="UnitsScalingFactor">The factor that turns the file's lengths into metres, or <see langword="null"/> when the file has no units.</param>
/// <param name="Nodes">The scene nodes.</param>
/// <param name="Geometry">The meshes, point sets and line sets.</param>
/// <param name="Resources">The lights, views, shaders, materials, textures and motions.</param>
/// <param name="BlockCount">The number of blocks read, counting those inside modifier chains.</param>
/// <param name="ContinuationBlocks">The number of blocks that hold shape, texture or priority data, which are kept but not decoded.</param>
/// <param name="Extensions">The names of the extensions the file declares, which are not read.</param>
/// <param name="Problems">What stopped the file from being read completely, in plain words; empty when it was read in full.</param>
[DebuggerDisplay("U3dScene: {Nodes.Length} nodes, {Geometry.Length} shapes")]
public sealed record U3dScene(
    short MajorVersion,
    short MinorVersion,
    uint Profile,
    double? UnitsScalingFactor,
    U3dNode[] Nodes,
    U3dGeometry[] Geometry,
    U3dResource[] Resources,
    int BlockCount,
    int ContinuationBlocks,
    string[] Extensions,
    string[] Problems)
{
    /// <summary>Gets a value indicating whether the whole file was read without a problem.</summary>
    public bool IsComplete => Problems.Length == 0;

    /// <summary>Counts the resources of one kind.</summary>
    /// <param name="kind">The block type of the resource.</param>
    /// <returns>The number of resources.</returns>
    public int CountResources(U3dBlockType kind)
    {
        var count = 0;
        foreach (var resource in Resources)
        {
            count += resource.Kind == kind ? 1 : 0;
        }

        return count;
    }

    /// <summary>Counts the nodes of one kind.</summary>
    /// <param name="kind">The kind of node.</param>
    /// <returns>The number of nodes.</returns>
    public int CountNodes(U3dNodeKind kind)
    {
        var count = 0;
        foreach (var node in Nodes)
        {
            count += node.Kind == kind ? 1 : 0;
        }

        return count;
    }
}
