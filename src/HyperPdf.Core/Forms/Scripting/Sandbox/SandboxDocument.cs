// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Forms.Scripting.Sandbox;

/// <summary>The document a form script runs in, as <c>this</c> gives it. It offers fields and nothing else.</summary>
/// <param name="host">The document.</param>
internal sealed class SandboxDocument(IFormScriptHost host) : SandboxObject
{
    /// <inheritdoc/>
    internal override SandboxValue Call(string name, ReadOnlySpan<SandboxValue> arguments)
    {
        if (name != "getField")
        {
            return base.Call(name, arguments);
        }

        var fieldName = !arguments.IsEmpty ? arguments[0].ToText() : string.Empty;
        return host.GetFieldValue(fieldName) is null ? SandboxValue.Null : SandboxValue.Of(new SandboxField(fieldName, host));
    }
}
