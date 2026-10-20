// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Media.U3d;

/// <summary>Checks that the nodes of a scene form a tree and not a loop (ECMA-363 9.5.1.2: a loop is an error).</summary>
internal static class U3dHierarchy
{
    /// <summary>A node not yet visited.</summary>
    private const byte Unvisited = 0;

    /// <summary>A node on the path being followed.</summary>
    private const byte OnPath = 1;

    /// <summary>A node whose parents have all been followed.</summary>
    private const byte Done = 2;

    /// <summary>The most nodes checked with stack space and a search by name; a larger scene uses arrays and a dictionary.</summary>
    private const int SmallScene = 64;

    /// <summary>Notes a problem when a node is its own ancestor.</summary>
    /// <param name="builder">Holds the nodes and receives the problem.</param>
    internal static void Check(U3dSceneBuilder builder)
    {
        var nodes = builder.Nodes;
        if (!HasNamedParent(nodes))
        {
            return;
        }

        var looped = nodes.Count <= SmallScene ? CheckSmall(nodes) : CheckLarge(nodes);
        if (looped)
        {
            builder.Problem("The scene's nodes form a loop, so the scene is not a tree.");
        }
    }

    /// <summary>Checks a small scene on the stack, finding parents by searching the nodes.</summary>
    /// <param name="nodes">The nodes.</param>
    /// <returns><see langword="true"/> when a loop was found.</returns>
    private static bool CheckSmall(List<U3dNode> nodes)
    {
        Span<byte> state = stackalloc byte[SmallScene];
        Span<int> path = stackalloc int[SmallScene];
        Span<int> next = stackalloc int[SmallScene];
        var looped = false;
        for (var start = 0; start < nodes.Count && !looped; start++)
        {
            looped = state[start] == Unvisited && Follows(nodes, null, state, path, next, start);
        }

        return looped;
    }

    /// <summary>Checks a larger scene, finding parents by name in a dictionary.</summary>
    /// <param name="nodes">The nodes.</param>
    /// <returns><see langword="true"/> when a loop was found.</returns>
    private static bool CheckLarge(List<U3dNode> nodes)
    {
        var index = new Dictionary<string, int>(nodes.Count, StringComparer.Ordinal);
        for (var i = 0; i < nodes.Count; i++)
        {
            _ = index.TryAdd(nodes[i].Name, i);
        }

        var state = new byte[nodes.Count];
        var path = new int[nodes.Count];
        var next = new int[nodes.Count];
        var looped = false;
        for (var start = 0; start < nodes.Count && !looped; start++)
        {
            looped = state[start] == Unvisited && Follows(nodes, index, state, path, next, start);
        }

        return looped;
    }

    /// <summary>Determines whether any node has a parent with a name; a scene without them cannot loop.</summary>
    /// <param name="nodes">The nodes.</param>
    /// <returns><see langword="true"/> when a node names a parent.</returns>
    private static bool HasNamedParent(List<U3dNode> nodes)
    {
        foreach (var node in nodes)
        {
            foreach (var parent in node.Parents)
            {
                if (parent.Length > 0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Finds the position of a node by name; the first node of a name wins.</summary>
    /// <param name="nodes">The nodes.</param>
    /// <param name="index">The positions by name, or <see langword="null"/> to search the nodes.</param>
    /// <param name="name">The name.</param>
    /// <returns>The position, or -1 when no node has the name.</returns>
    private static int Find(List<U3dNode> nodes, Dictionary<string, int>? index, string name)
    {
        if (index is not null)
        {
            return index.GetValueOrDefault(name, -1);
        }

        for (var i = 0; i < nodes.Count; i++)
        {
            if (string.Equals(nodes[i].Name, name, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Follows the parents of a node, without recursion, looking for a loop.</summary>
    /// <param name="nodes">The nodes.</param>
    /// <param name="index">The position of each node by name, or <see langword="null"/> to search the nodes.</param>
    /// <param name="state">What has been visited.</param>
    /// <param name="path">The nodes on the current path.</param>
    /// <param name="next">The next parent to follow for each node on the path.</param>
    /// <param name="start">The node to start from.</param>
    /// <returns><see langword="true"/> when a loop was found.</returns>
    private static bool Follows(List<U3dNode> nodes, Dictionary<string, int>? index, Span<byte> state, Span<int> path, Span<int> next, int start)
    {
        var depth = 0;
        path[0] = start;
        next[0] = 0;
        state[start] = OnPath;
        while (depth >= 0)
        {
            var node = path[depth];
            var parents = nodes[node].Parents;
            if (next[depth] >= parents.Length)
            {
                state[node] = Done;
                depth--;
                continue;
            }

            var parent = Find(nodes, index, parents[next[depth]]);
            next[depth]++;
            if (parent >= 0 && state[parent] == OnPath)
            {
                return true;
            }

            if (parent < 0 || state[parent] != Unvisited)
            {
                continue;
            }

            depth++;
            path[depth] = parent;
            next[depth] = 0;
            state[parent] = OnPath;
        }

        return false;
    }
}
