// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Forms.Scripting;

/// <summary>The scripts a form field's widget carries, each recognised or <see cref="FormScript.None"/>.</summary>
/// <param name="PageIndex">The zero based page index.</param>
/// <param name="Index">The widget's annotation index.</param>
/// <param name="Name">The field's full name.</param>
/// <param name="Keystroke">Checks what is typed.</param>
/// <param name="Format">Formats the value for display.</param>
/// <param name="Validate">Validates a new value.</param>
/// <param name="Calculate">Calculates the value from other fields.</param>
[DebuggerDisplay("FieldScripts: {Name}")]
public sealed record FieldScripts(int PageIndex, int Index, string Name, FormScript Keystroke, FormScript Format, FormScript Validate, FormScript Calculate)
{
    /// <summary>Gets a value indicating whether the field has any script HyperPDF runs.</summary>
    public bool HasAny =>
        Keystroke.Function != FormScriptFunction.Unknown || Format.Function != FormScriptFunction.Unknown
        || Validate.Function != FormScriptFunction.Unknown || Calculate.Function != FormScriptFunction.Unknown;
}
