// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Forms.Scripting.Sandbox;

/// <summary>
/// Checks a parsed script before it is ever run: every name it uses must be a variable it declares or an object the sandbox
/// has, and every call on one of those objects must be a method the sandbox has. A script that is refused here is reported as
/// not run, rather than being half run.
/// </summary>
internal static class SandboxValidator
{
    /// <summary>The names of the objects and words every script can use.</summary>
    private static readonly string[] Roots = ["this", "event", "app", "util", "Math", "display", "true", "false", "null", "undefined"];

    /// <summary>The global functions every script can call.</summary>
    private static readonly string[] Functions = ["parseInt", "parseFloat", "Number", "String", "Boolean", "isNaN"];

    /// <summary>The methods of <c>Math</c>.</summary>
    private static readonly string[] MathMethods = ["abs", "floor", "ceil", "sqrt", "trunc", "round", "pow", "min", "max"];

    /// <summary>Refuses a script that uses a name or method the sandbox does not have.</summary>
    /// <param name="root">The parsed script.</param>
    /// <exception cref="SandboxException">The script uses something the sandbox does not have.</exception>
    internal static void Check(SandboxNode root)
    {
        var declared = new HashSet<string>(StringComparer.Ordinal);
        Collect(root, declared);
        Verify(root, declared);
    }

    /// <summary>Gathers the variables a script declares or assigns, wherever they come in the script.</summary>
    /// <param name="node">The node.</param>
    /// <param name="declared">Receives the names.</param>
    private static void Collect(SandboxNode node, HashSet<string> declared)
    {
        if (node.Kind == SandboxNodeKind.Declare)
        {
            _ = declared.Add(node.Text);
        }

        if (node.Kind is SandboxNodeKind.Assign or SandboxNodeKind.Update && node.Children[0].Kind == SandboxNodeKind.Name)
        {
            _ = declared.Add(node.Children[0].Text);
        }

        foreach (var child in node.Children)
        {
            Collect(child, declared);
        }
    }

    /// <summary>Checks the names and calls of a node and the nodes below it.</summary>
    /// <param name="node">The node.</param>
    /// <param name="declared">The variables the script declares.</param>
    /// <exception cref="SandboxException">The script uses something the sandbox does not have.</exception>
    private static void Verify(SandboxNode node, HashSet<string> declared)
    {
        if (node.Kind == SandboxNodeKind.Name && !declared.Contains(node.Text) && Array.IndexOf(Roots, node.Text) < 0)
        {
            throw new SandboxException($"The script uses '{node.Text}', which is not available to form scripts.");
        }

        var first = 0;
        if (node.Kind == SandboxNodeKind.Call)
        {
            VerifyCall(node.Children[0], declared);

            // The name of a global function is checked as a callee, and is not a variable.
            first = node.Children[0].Kind == SandboxNodeKind.Name ? 1 : 0;
        }

        for (var i = first; i < node.Children.Length; i++)
        {
            Verify(node.Children[i], declared);
        }
    }

    /// <summary>Checks the callee of a call.</summary>
    /// <param name="callee">The callee node.</param>
    /// <param name="declared">The variables the script declares.</param>
    /// <exception cref="SandboxException">The callee is not a function or method the sandbox has.</exception>
    private static void VerifyCall(SandboxNode callee, HashSet<string> declared)
    {
        if (callee.Kind == SandboxNodeKind.Name && Array.IndexOf(Functions, callee.Text) < 0)
        {
            throw new SandboxException($"The script calls {callee.Text}, which form scripts cannot use.");
        }

        if (callee.Kind == SandboxNodeKind.Member && callee.Children[0] is { Kind: SandboxNodeKind.Name } owner && !declared.Contains(owner.Text) && !Allows(owner.Text, callee.Text))
        {
            throw new SandboxException($"The script calls {owner.Text}.{callee.Text}, which form scripts cannot use.");
        }
    }

    /// <summary>Determines whether the sandbox has a method on one of its objects.</summary>
    /// <param name="owner">The object's name.</param>
    /// <param name="method">The method.</param>
    /// <returns><see langword="true"/> when the method exists.</returns>
    private static bool Allows(string owner, string method) => owner switch
    {
        "this" => method == "getField",
        "app" => method == "alert",
        "util" => method == "printf",
        "Math" => Array.IndexOf(MathMethods, method) >= 0,
        _ => false,
    };
}
