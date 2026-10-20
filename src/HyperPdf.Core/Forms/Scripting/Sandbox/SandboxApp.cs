// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Forms.Scripting.Sandbox;

/// <summary>The <c>app</c> object of a form script. It can only leave a message for the reader.</summary>
/// <param name="messages">Receives the messages.</param>
internal sealed class SandboxApp(List<string> messages) : SandboxObject
{
    /// <inheritdoc/>
    internal override SandboxValue Call(string name, SandboxValue[] arguments)
    {
        if (name != "alert")
        {
            return base.Call($"app.{name}", arguments);
        }

        messages.Add(arguments.Length > 0 ? arguments[0].ToText() : string.Empty);
        return SandboxValue.Of(1);
    }
}
