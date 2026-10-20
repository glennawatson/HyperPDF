// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdf.Core.Forms.Scripting;

/// <summary>A form with no fields, for scripts that do not reach into the form.</summary>
public sealed class EmptyFormScriptHost : IFormScriptHost
{
    /// <summary>Gets the one empty form.</summary>
    public static EmptyFormScriptHost Instance { get; } = new();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string? GetFieldValue(string fieldName) => null;

    /// <inheritdoc/>
    public void SetFieldValue(string fieldName, string value)
    {
    }

    /// <inheritdoc/>
    public void SetFieldDisplay(string fieldName, FormFieldDisplay display)
    {
    }

    /// <inheritdoc/>
    public void SetFieldReadOnly(string fieldName, bool readOnly)
    {
    }
}
