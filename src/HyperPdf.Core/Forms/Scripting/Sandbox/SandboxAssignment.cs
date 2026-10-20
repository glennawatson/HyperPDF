// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Forms.Scripting.Sandbox;

/// <summary>Stores values for the assignments of a form script.</summary>
internal static class SandboxAssignment
{
    /// <summary>Stores a value in a variable, a property of an object, or an element of an array.</summary>
    /// <param name="target">The target node: a name, member or index.</param>
    /// <param name="value">The value.</param>
    /// <param name="context">The run.</param>
    /// <exception cref="SandboxException">The object cannot be changed by scripts.</exception>
    internal static void Store(SandboxNode target, SandboxValue value, SandboxContext context)
    {
        if (target.Kind == SandboxNodeKind.Name)
        {
            context.SetVariable((int)target.Number, value);
            return;
        }

        var owner = SandboxInterpreter.Evaluate(target.Children[0], context).AsObject() ?? throw new SandboxException("The script changes something that is not an object.");
        if (target.Kind == SandboxNodeKind.Member)
        {
            owner.Set(target.Text, value);
            return;
        }

        var index = SandboxInterpreter.Evaluate(target.Children[1], context);
        if (index.Kind == SandboxValueKind.Number)
        {
            owner.SetAt((int)index.Number, value);
            return;
        }

        owner.Set(index.ToText(), value);
    }
}
