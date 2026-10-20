// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Forms.Scripting.Sandbox;

/// <summary>The <c>util</c> object of a form script. It formats text and nothing else.</summary>
internal sealed class SandboxUtil : SandboxObject
{
    /// <inheritdoc/>
    internal override SandboxValue Call(string name, ReadOnlySpan<SandboxValue> arguments) =>
        name == "printf" && !arguments.IsEmpty
            ? SandboxValue.Of(SandboxPrintf.Format(arguments[0].ToText(), arguments[1..]))
            : base.Call($"util.{name}", arguments);
}
