// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Forms.Scripting.Sandbox;

/// <summary>An object a form script can reach: only the objects the sandbox hands out, never the app's own. By default it has nothing.</summary>
internal class SandboxObject
{
    /// <summary>Reads a property.</summary>
    /// <param name="name">The property's name.</param>
    /// <returns>The value; undefined when the object has no such property.</returns>
    internal virtual SandboxValue Get(string name) => SandboxValue.Undefined;

    /// <summary>Writes a property.</summary>
    /// <param name="name">The property's name.</param>
    /// <param name="value">The value.</param>
    internal virtual void Set(string name, SandboxValue value)
    {
    }

    /// <summary>Calls a method.</summary>
    /// <param name="name">The method's name.</param>
    /// <param name="arguments">The arguments.</param>
    /// <returns>The result.</returns>
    /// <exception cref="SandboxException">The object has no such method.</exception>
    internal virtual SandboxValue Call(string name, SandboxValue[] arguments) =>
        throw new SandboxException($"The script calls {name}, which form scripts cannot use.");

    /// <summary>Reads an element by position.</summary>
    /// <param name="index">The position.</param>
    /// <returns>The value; undefined when there is no such element.</returns>
    internal virtual SandboxValue GetAt(int index) => SandboxValue.Undefined;

    /// <summary>Writes an element by position.</summary>
    /// <param name="index">The position.</param>
    /// <param name="value">The value.</param>
    internal virtual void SetAt(int index, SandboxValue value)
    {
    }

    /// <summary>Writes the object as text.</summary>
    /// <returns>The text.</returns>
    internal virtual string ToText() => "[object Object]";
}
