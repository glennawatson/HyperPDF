// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Forms.Scripting.Sandbox;

/// <summary>Runs a parsed form script by walking its tree.</summary>
internal static class SandboxInterpreter
{
    /// <summary>What runs each kind of node, in the order of <see cref="SandboxNodeKind"/>.</summary>
    private static readonly Func<SandboxNode, SandboxContext, SandboxValue>[] Handlers =
    [
        SandboxStatements.Block,
        SandboxStatements.Declare,
        SandboxStatements.If,
        SandboxStatements.For,
        SandboxStatements.While,
        SandboxStatements.Break,
        SandboxStatements.Continue,
        SandboxExpressions.Number,
        SandboxExpressions.Text,
        SandboxExpressions.Name,
        SandboxExpressions.Name,
        SandboxExpressions.Member,
        SandboxExpressions.Index,
        SandboxExpressions.Call,
        SandboxExpressions.Unary,
        SandboxExpressions.Update,
        SandboxExpressions.Binary,
        SandboxExpressions.Logical,
        SandboxExpressions.Conditional,
        SandboxExpressions.Assign,
        SandboxExpressions.Array,
    ];

    /// <summary>Runs a node.</summary>
    /// <param name="node">The node.</param>
    /// <param name="context">The run.</param>
    /// <returns>The value of an expression; undefined for a statement.</returns>
    /// <exception cref="SandboxException">The script takes too many steps or uses something the sandbox does not allow.</exception>
    internal static SandboxValue Evaluate(SandboxNode node, SandboxContext context)
    {
        context.Step();
        return Handlers[(int)node.Kind](node, context);
    }
}
