// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Forms.Scripting.Sandbox;

/// <summary>A field of the form, as <c>this.getField("name")</c> gives it: its value, whether it shows, and whether it can be edited.</summary>
/// <param name="fieldName">The field's full name.</param>
/// <param name="host">The document the field is in.</param>
internal sealed class SandboxField(string fieldName, IFormScriptHost host) : SandboxObject
{
    /// <inheritdoc/>
    internal override SandboxValue Get(string name) => name switch
    {
        "value" => ReadValue(),
        "name" => SandboxValue.Of(fieldName),
        "display" => SandboxValue.Of((double)FormFieldDisplay.Visible),
        "readonly" => SandboxValue.False,
        _ => SandboxValue.Undefined,
    };

    /// <inheritdoc/>
    internal override void Set(string name, SandboxValue value)
    {
        switch (name)
        {
            case "value":
            {
                host.SetFieldValue(fieldName, value.ToText());
                break;
            }

            case "display":
            {
                host.SetFieldDisplay(fieldName, ToDisplay(value));
                break;
            }

            case "readonly":
            {
                host.SetFieldReadOnly(fieldName, value.IsTruthy);
                break;
            }
        }
    }

    /// <summary>Converts a script's value to a display setting; unknown numbers show the field.</summary>
    /// <param name="value">The value, one of the <c>display</c> constants.</param>
    /// <returns>The setting.</returns>
    private static FormFieldDisplay ToDisplay(SandboxValue value)
    {
        var display = (FormFieldDisplay)(int)value.ToNumber();
        return Enum.IsDefined(display) ? display : FormFieldDisplay.Visible;
    }

    /// <summary>Reads the value the way a form field gives it: a number when the text is one, otherwise the text.</summary>
    /// <returns>The value.</returns>
    private SandboxValue ReadValue()
    {
        var text = host.GetFieldValue(fieldName) ?? string.Empty;
        var number = SandboxValue.TextToNumber(text);
        return text.Length > 0 && !double.IsNaN(number) ? SandboxValue.Of(number) : SandboxValue.Of(text);
    }
}
