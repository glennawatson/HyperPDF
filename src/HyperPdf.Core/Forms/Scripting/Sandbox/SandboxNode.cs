// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Forms.Scripting.Sandbox;

/// <summary>One node of a parsed form script.</summary>
/// <param name="Kind">What the node is.</param>
/// <param name="Text">The name, operator or string of the node.</param>
/// <param name="Number">The value of a number literal; 1 for a prefix update.</param>
/// <param name="Children">The nodes below this one.</param>
[DebuggerDisplay("SandboxNode: {Kind} {Text}")]
internal sealed record SandboxNode(SandboxNodeKind Kind, string Text, double Number, SandboxNode[] Children)
{
    /// <summary>The deepest tree of nodes allowed, so that walking a tree can never use up the stack.</summary>
    internal const int MaxTreeDepth = 128;

    /// <summary>Gets the depth of the tree this node is the top of.</summary>
    internal int Depth { get; } = DepthOf(Children);

    /// <summary>Makes a node with a text and children.</summary>
    /// <param name="kind">What the node is.</param>
    /// <param name="text">The name, operator or string.</param>
    /// <param name="children">The nodes below it.</param>
    /// <returns>The node.</returns>
    internal static SandboxNode Of(SandboxNodeKind kind, string text, params SandboxNode[] children) => new(kind, text, 0, children);

    /// <summary>Makes a node with children only.</summary>
    /// <param name="kind">What the node is.</param>
    /// <param name="children">The nodes below it.</param>
    /// <returns>The node.</returns>
    internal static SandboxNode Of(SandboxNodeKind kind, params SandboxNode[] children) => new(kind, string.Empty, 0, children);

    /// <summary>Works out the depth of a node from its children.</summary>
    /// <param name="children">The children.</param>
    /// <returns>The depth, which is one for a node with no children.</returns>
    /// <exception cref="SandboxException">The tree is deeper than the sandbox allows.</exception>
    private static int DepthOf(SandboxNode[] children)
    {
        var deepest = 0;
        foreach (var child in children)
        {
            deepest = Math.Max(deepest, child.Depth);
        }

        return deepest < MaxTreeDepth ? deepest + 1 : throw new SandboxException("The script nests too deeply to run.");
    }
}
