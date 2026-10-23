// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Forms.Scripting;

namespace HyperPdf.Core.Tests.Forms;

/// <summary>A form held in memory for the sandboxed script tests, remembering what the scripts change.</summary>
internal sealed class FakeScriptHost : IFormScriptHost
{
    /// <summary>Gets the field values by name.</summary>
    internal Dictionary<string, string> Values { get; } = [with(StringComparer.Ordinal)];

    /// <summary>Gets where the scripts put each field.</summary>
    internal Dictionary<string, FormFieldDisplay> Displays { get; } = [with(StringComparer.Ordinal)];

    /// <summary>Gets the fields the scripts made read-only, with the setting.</summary>
    internal Dictionary<string, bool> ReadOnly { get; } = [with(StringComparer.Ordinal)];

    /// <summary>Gets the messages the scripts told the reader.</summary>
    internal List<string> Told { get; } = [];

    /// <inheritdoc/>
    public string? GetFieldValue(string fieldName) => Values.GetValueOrDefault(fieldName);

    /// <inheritdoc/>
    public void SetFieldValue(string fieldName, string value) => Values[fieldName] = value;

    /// <inheritdoc/>
    public void SetFieldDisplay(string fieldName, FormFieldDisplay display) => Displays[fieldName] = display;

    /// <inheritdoc/>
    public void SetFieldReadOnly(string fieldName, bool readOnly) => ReadOnly[fieldName] = readOnly;

    /// <inheritdoc/>
    public void Tell(string message) => Told.Add(message);
}
