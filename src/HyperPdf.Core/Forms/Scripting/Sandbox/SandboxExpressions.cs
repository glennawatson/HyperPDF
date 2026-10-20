// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdf.Core.Forms.Scripting.Sandbox;

/// <summary>Runs the expressions of a form script.</summary>
internal static class SandboxExpressions
{
    /// <summary>The position of the value for a true test in a choice node.</summary>
    private const int ThenBranch = 1;

    /// <summary>The position of the value for a false test in a choice node.</summary>
    private const int ElseBranch = 2;

    /// <summary>Gives a number literal.</summary>
    /// <param name="node">The literal.</param>
    /// <param name="context">The run.</param>
    /// <returns>The number.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static SandboxValue Number(SandboxNode node, SandboxContext context) => SandboxValue.Of(node.Number);

    /// <summary>Gives a string literal.</summary>
    /// <param name="node">The literal.</param>
    /// <param name="context">The run.</param>
    /// <returns>The text.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static SandboxValue Text(SandboxNode node, SandboxContext context) => SandboxValue.Of(node.Text);

    /// <summary>Reads a variable, a global object, or one of the words <c>true</c>, <c>false</c>, <c>null</c> and <c>undefined</c>.</summary>
    /// <param name="node">The name.</param>
    /// <param name="context">The run.</param>
    /// <returns>The value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static SandboxValue Name(SandboxNode node, SandboxContext context) => context.Lookup(node.Text);

    /// <summary>Reads a property.</summary>
    /// <param name="node">The member read: the object.</param>
    /// <param name="context">The run.</param>
    /// <returns>The value.</returns>
    internal static SandboxValue Member(SandboxNode node, SandboxContext context)
    {
        var target = SandboxInterpreter.Evaluate(node.Children[0], context);
        return target.AsObject() is { } owner ? owner.Get(node.Text) : SandboxPrimitives.Get(target, node.Text);
    }

    /// <summary>Reads an element of an array or a character of text.</summary>
    /// <param name="node">The element read: the object and the index.</param>
    /// <param name="context">The run.</param>
    /// <returns>The value.</returns>
    internal static SandboxValue Index(SandboxNode node, SandboxContext context)
    {
        var target = SandboxInterpreter.Evaluate(node.Children[0], context);
        var index = SandboxInterpreter.Evaluate(node.Children[1], context);
        if (target.AsObject() is { } owner)
        {
            return index.Kind == SandboxValueKind.Number ? owner.GetAt((int)index.Number) : owner.Get(index.ToText());
        }

        return target.IsText && index.Kind == SandboxValueKind.Number
            ? SandboxValue.Of(SandboxPrimitives.CharAt((string)target.Reference!, (int)index.Number))
            : SandboxValue.Undefined;
    }

    /// <summary>Calls a method of an object or text, or a global function.</summary>
    /// <param name="node">The call: the callee and then the arguments.</param>
    /// <param name="context">The run.</param>
    /// <returns>The result.</returns>
    /// <exception cref="SandboxException">The callee is not a method or global function the sandbox has.</exception>
    internal static SandboxValue Call(SandboxNode node, SandboxContext context)
    {
        var callee = node.Children[0];
        if (callee.Kind is not (SandboxNodeKind.Member or SandboxNodeKind.Name))
        {
            throw new SandboxException("The script calls something that form scripts cannot call.");
        }

        var receiver = callee.Kind == SandboxNodeKind.Member ? SandboxInterpreter.Evaluate(callee.Children[0], context) : SandboxValue.Undefined;
        var arguments = new SandboxValue[node.Children.Length - 1];
        for (var i = 0; i < arguments.Length; i++)
        {
            arguments[i] = SandboxInterpreter.Evaluate(node.Children[i + 1], context);
        }

        if (callee.Kind == SandboxNodeKind.Name)
        {
            return SandboxPrimitives.CallGlobal(callee.Text, arguments);
        }

        return receiver.AsObject() is { } owner ? owner.Call(callee.Text, arguments) : SandboxPrimitives.Call(receiver, callee.Text, arguments);
    }

    /// <summary>Applies <c>!</c>, <c>-</c>, <c>+</c> or <c>typeof</c>.</summary>
    /// <param name="node">The operation: the operand.</param>
    /// <param name="context">The run.</param>
    /// <returns>The result.</returns>
    internal static SandboxValue Unary(SandboxNode node, SandboxContext context)
    {
        var operand = SandboxInterpreter.Evaluate(node.Children[0], context);
        return node.Text switch
        {
            "!" => SandboxValue.Of(!operand.IsTruthy),
            "-" => SandboxValue.Of(-operand.ToNumber()),
            "+" => SandboxValue.Of(operand.ToNumber()),
            _ => SandboxValue.Of(TypeName(operand)),
        };
    }

    /// <summary>Increases or decreases a variable, property or element by one.</summary>
    /// <param name="node">The update: the target. Its number is 1 for a prefix operator.</param>
    /// <param name="context">The run.</param>
    /// <returns>The new value for a prefix operator, otherwise the old one.</returns>
    internal static SandboxValue Update(SandboxNode node, SandboxContext context)
    {
        var old = SandboxInterpreter.Evaluate(node.Children[0], context).ToNumber();
        var updated = SandboxValue.Of(node.Text == "++" ? old + 1 : old - 1);
        SandboxAssignment.Store(node.Children[0], updated, context);
        return node.Number != 0 ? updated : SandboxValue.Of(old);
    }

    /// <summary>Applies a binary operator.</summary>
    /// <param name="node">The operation: left and right.</param>
    /// <param name="context">The run.</param>
    /// <returns>The result.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static SandboxValue Binary(SandboxNode node, SandboxContext context) =>
        SandboxOperators.Apply(node.Text, SandboxInterpreter.Evaluate(node.Children[0], context), SandboxInterpreter.Evaluate(node.Children[1], context));

    /// <summary>Applies <c>&amp;&amp;</c> or <c>||</c>, reading the right side only when it can change the result.</summary>
    /// <param name="node">The operation: left and right.</param>
    /// <param name="context">The run.</param>
    /// <returns>The left or right value.</returns>
    internal static SandboxValue Logical(SandboxNode node, SandboxContext context)
    {
        var left = SandboxInterpreter.Evaluate(node.Children[0], context);
        var decided = node.Text == "&&" ? !left.IsTruthy : left.IsTruthy;
        return decided ? left : SandboxInterpreter.Evaluate(node.Children[1], context);
    }

    /// <summary>Picks one of two values by a test.</summary>
    /// <param name="node">The choice: test, then, else.</param>
    /// <param name="context">The run.</param>
    /// <returns>The chosen value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static SandboxValue Conditional(SandboxNode node, SandboxContext context) =>
        SandboxInterpreter.Evaluate(node.Children[SandboxInterpreter.Evaluate(node.Children[0], context).IsTruthy ? ThenBranch : ElseBranch], context);

    /// <summary>Stores a value in a variable, property or element.</summary>
    /// <param name="node">The assignment: target and value. The text is <c>=</c> or a compound operator.</param>
    /// <param name="context">The run.</param>
    /// <returns>The stored value.</returns>
    internal static SandboxValue Assign(SandboxNode node, SandboxContext context)
    {
        var value = SandboxInterpreter.Evaluate(node.Children[1], context);
        if (node.Text != "=")
        {
            var current = SandboxInterpreter.Evaluate(node.Children[0], context);
            value = SandboxOperators.Apply(node.Text[..^1], current, value);
        }

        SandboxAssignment.Store(node.Children[0], value, context);
        return value;
    }

    /// <summary>Makes an array.</summary>
    /// <param name="node">The array: its elements.</param>
    /// <param name="context">The run.</param>
    /// <returns>The array.</returns>
    internal static SandboxValue Array(SandboxNode node, SandboxContext context)
    {
        var array = new SandboxArray();
        for (var i = 0; i < node.Children.Length; i++)
        {
            array.SetAt(i, SandboxInterpreter.Evaluate(node.Children[i], context));
        }

        return SandboxValue.Of(array);
    }

    /// <summary>Names the kind of a value as <c>typeof</c> does.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The name.</returns>
    private static string TypeName(SandboxValue value) => value.Kind switch
    {
        SandboxValueKind.Undefined => "undefined",
        SandboxValueKind.Boolean => "boolean",
        SandboxValueKind.Number => "number",
        SandboxValueKind.String => "string",
        _ => "object",
    };
}
