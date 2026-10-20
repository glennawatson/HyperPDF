// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Forms.Scripting.Sandbox;

/// <summary>Runs the statements of a form script: blocks, declarations, <c>if</c>, loops, <c>break</c> and <c>continue</c>.</summary>
internal static class SandboxStatements
{
    /// <summary>Runs each statement of a block until one asks to leave it.</summary>
    /// <param name="node">The block.</param>
    /// <param name="context">The run.</param>
    /// <returns>Undefined.</returns>
    internal static SandboxValue Block(SandboxNode node, SandboxContext context)
    {
        foreach (var statement in node.Children)
        {
            _ = SandboxInterpreter.Evaluate(statement, context);
            if (context.Signal != SandboxSignal.None)
            {
                break;
            }
        }

        return SandboxValue.Undefined;
    }

    /// <summary>Declares a variable.</summary>
    /// <param name="node">The declaration.</param>
    /// <param name="context">The run.</param>
    /// <returns>Undefined.</returns>
    internal static SandboxValue Declare(SandboxNode node, SandboxContext context)
    {
        context.SetVariable((int)node.Number, SandboxInterpreter.Evaluate(node.Children[0], context));
        return SandboxValue.Undefined;
    }

    /// <summary>Runs one branch of an <c>if</c>.</summary>
    /// <param name="node">The <c>if</c>: test, then, and optionally else.</param>
    /// <param name="context">The run.</param>
    /// <returns>Undefined.</returns>
    internal static SandboxValue If(SandboxNode node, SandboxContext context)
    {
        if (SandboxInterpreter.Evaluate(node.Children[0], context).IsTruthy)
        {
            _ = SandboxInterpreter.Evaluate(node.Children[1], context);
        }
        else if (node.Children.Length > 2)
        {
            _ = SandboxInterpreter.Evaluate(node.Children[2], context);
        }

        return SandboxValue.Undefined;
    }

    /// <summary>Runs a <c>while</c> loop.</summary>
    /// <param name="node">The loop: test and body.</param>
    /// <param name="context">The run.</param>
    /// <returns>Undefined.</returns>
    internal static SandboxValue While(SandboxNode node, SandboxContext context)
    {
        var running = true;
        while (running)
        {
            running = SandboxInterpreter.Evaluate(node.Children[0], context).IsTruthy && RunBody(node.Children[1], context);
        }

        return SandboxValue.Undefined;
    }

    /// <summary>Runs a <c>for</c> loop.</summary>
    /// <param name="node">The loop: start, test, step and body.</param>
    /// <param name="context">The run.</param>
    /// <returns>Undefined.</returns>
    internal static SandboxValue For(SandboxNode node, SandboxContext context)
    {
        _ = SandboxInterpreter.Evaluate(node.Children[0], context);
        while (SandboxInterpreter.Evaluate(node.Children[1], context).IsTruthy && RunBody(node.Children[3], context))
        {
            _ = SandboxInterpreter.Evaluate(node.Children[2], context);
        }

        return SandboxValue.Undefined;
    }

    /// <summary>Asks the loop to stop.</summary>
    /// <param name="node">The <c>break</c>.</param>
    /// <param name="context">The run.</param>
    /// <returns>Undefined.</returns>
    internal static SandboxValue Break(SandboxNode node, SandboxContext context)
    {
        context.Signal = SandboxSignal.Break;
        return SandboxValue.Undefined;
    }

    /// <summary>Asks the loop to go on to its next round.</summary>
    /// <param name="node">The <c>continue</c>.</param>
    /// <param name="context">The run.</param>
    /// <returns>Undefined.</returns>
    internal static SandboxValue Continue(SandboxNode node, SandboxContext context)
    {
        context.Signal = SandboxSignal.Continue;
        return SandboxValue.Undefined;
    }

    /// <summary>Runs a loop body once and reads what it asked for.</summary>
    /// <param name="body">The body.</param>
    /// <param name="context">The run.</param>
    /// <returns><see langword="true"/> when the loop goes on.</returns>
    private static bool RunBody(SandboxNode body, SandboxContext context)
    {
        _ = SandboxInterpreter.Evaluate(body, context);
        var stop = context.Signal == SandboxSignal.Break;
        context.Signal = SandboxSignal.None;
        return !stop;
    }
}
