// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Forms.Scripting.Sandbox;

/// <summary>An object that is only a set of named values, such as <c>event</c> and <c>display</c>.</summary>
internal sealed class SandboxBag : SandboxObject
{
    /// <summary>The values by name.</summary>
    private readonly Dictionary<string, SandboxValue> _values = [with(StringComparer.Ordinal)];

    /// <summary>Sets a value while the sandbox is being prepared.</summary>
    /// <param name="name">The name.</param>
    /// <param name="value">The value.</param>
    /// <returns>The bag, so setup reads as one chain.</returns>
    internal SandboxBag With(string name, SandboxValue value)
    {
        _values[name] = value;
        return this;
    }

    /// <inheritdoc/>
    internal override SandboxValue Get(string name) => _values.GetValueOrDefault(name, SandboxValue.Undefined);

    /// <inheritdoc/>
    internal override void Set(string name, SandboxValue value) => _values[name] = value;
}
