// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Media.U3d;

/// <summary>
/// Works out where each scene node is in the world. A node may have several parents, and is then in the world once for each path to
/// the root; loops are cut, and the number of places is limited.
/// </summary>
internal sealed class U3dSceneGraph
{
    /// <summary>The deepest chain of parents followed.</summary>
    private const int MaxDepth = 256;

    /// <summary>The nodes.</summary>
    private readonly U3dSceneNode[] _nodes;

    /// <summary>The most places the nodes are in the world, all together.</summary>
    private readonly int _maxInstances;

    /// <summary>The index of the first node of each name.</summary>
    private readonly Dictionary<string, int> _byName = [];

    /// <summary>The places of each node, once worked out.</summary>
    private readonly Matrix4x4[]?[] _places;

    /// <summary>The nodes whose parents are being followed, which marks a loop when reached again.</summary>
    private readonly bool[] _following;

    /// <summary>How many places have been worked out.</summary>
    private int _placed;

    /// <summary>Initializes a new instance of the <see cref="U3dSceneGraph"/> class.</summary>
    /// <param name="nodes">The scene nodes.</param>
    /// <param name="maxInstances">The most places the nodes may be in the world, all together.</param>
    internal U3dSceneGraph(U3dSceneNode[] nodes, int maxInstances)
    {
        _nodes = nodes;
        _maxInstances = maxInstances;
        _places = new Matrix4x4[]?[nodes.Length];
        _following = new bool[nodes.Length];
        for (var i = 0; i < nodes.Length; i++)
        {
            _ = _byName.TryAdd(nodes[i].Name, i);
        }
    }

    /// <summary>Gets the places of a node in the world.</summary>
    /// <param name="index">The node's index.</param>
    /// <returns>The world matrix for each path to the root; the identity for a node without parents.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Matrix4x4[] PlacesOf(int index) => Place(index, 0);

    /// <summary>Works out the places of a node, following its parents.</summary>
    /// <param name="index">The node's index.</param>
    /// <param name="depth">How many children are between this node and the one asked for.</param>
    /// <returns>The places.</returns>
    private Matrix4x4[] Place(int index, int depth)
    {
        if (_places[index] is { } known)
        {
            return known;
        }

        var node = _nodes[index];
        if (_following[index] || depth > MaxDepth || node.Parents.Length == 0)
        {
            return [Matrix4x4.Identity];
        }

        _following[index] = true;
        var places = new List<Matrix4x4>();
        for (var parent = 0; parent < node.Parents.Length && _placed < _maxInstances; parent++)
        {
            AddPlaces(places, node.Transforms[parent], node.Parents[parent], depth);
        }

        _following[index] = false;
        _placed += places.Count;
        var result = places.Count == 0 ? [Matrix4x4.Identity] : places.ToArray();
        _places[index] = result;
        return result;
    }

    /// <summary>Adds the places a transform gives a node under one parent.</summary>
    /// <param name="places">Receives the places.</param>
    /// <param name="local">The transform of the node relative to the parent.</param>
    /// <param name="parent">The parent's name; empty or unknown names the root of the scene.</param>
    /// <param name="depth">The depth so far.</param>
    private void AddPlaces(List<Matrix4x4> places, Matrix4x4 local, string parent, int depth)
    {
        if (parent.Length == 0 || !_byName.TryGetValue(parent, out var parentIndex))
        {
            places.Add(local);
            return;
        }

        foreach (var world in Place(parentIndex, depth + 1))
        {
            places.Add(local * world);
        }
    }
}
