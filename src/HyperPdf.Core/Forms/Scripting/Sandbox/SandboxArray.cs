// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Forms.Scripting.Sandbox;

/// <summary>An array made by a form script.</summary>
internal sealed class SandboxArray : SandboxObject
{
    /// <summary>The most elements an array may hold, so a script cannot use up memory.</summary>
    internal const int MaxLength = 10_000;

    /// <summary>The elements.</summary>
    private readonly List<SandboxValue> _items = [];

    /// <inheritdoc/>
    internal override SandboxValue Get(string name) => name == "length" ? SandboxValue.Of(_items.Count) : SandboxValue.Undefined;

    /// <inheritdoc/>
    internal override SandboxValue GetAt(int index) => (uint)index < (uint)_items.Count ? _items[index] : SandboxValue.Undefined;

    /// <inheritdoc/>
    internal override void SetAt(int index, SandboxValue value)
    {
        if ((uint)index >= MaxLength)
        {
            throw new SandboxException("The script makes an array that is too large.");
        }

        while (_items.Count <= index)
        {
            _items.Add(SandboxValue.Undefined);
        }

        _items[index] = value;
    }

    /// <inheritdoc/>
    internal override SandboxValue Call(string name, ReadOnlySpan<SandboxValue> arguments)
    {
        if (name == "push")
        {
            foreach (var argument in arguments)
            {
                SetAt(_items.Count, argument);
            }

            return SandboxValue.Of(_items.Count);
        }

        return name == "join" ? SandboxValue.Of(Join(!arguments.IsEmpty ? arguments[0].ToText() : ",")) : base.Call(name, arguments);
    }

    /// <inheritdoc/>
    internal override string ToText() => Join(",");

    /// <summary>Joins the elements as text.</summary>
    /// <param name="separator">The text between elements.</param>
    /// <returns>The text.</returns>
    private string Join(string separator)
    {
        var parts = new string[_items.Count];
        for (var i = 0; i < parts.Length; i++)
        {
            parts[i] = _items[i].Kind is SandboxValueKind.Undefined or SandboxValueKind.Null ? string.Empty : _items[i].ToText();
        }

        return string.Join(separator, parts);
    }
}
