// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Forms.Scripting;

/// <summary>
/// Runs a field's four scripts, whether they are one of the built-in form functions or a sandboxed program. A program that
/// cannot be run, because it goes beyond the sandbox, leaves the value as it was and never blocks the reader.
/// </summary>
public static class FormScriptRuns
{
    /// <summary>The message shown when a script refuses a value without saying why.</summary>
    private const string DefaultRefusal = "The form does not accept that value.";

    /// <summary>Formats a value for display.</summary>
    /// <param name="format">The format script.</param>
    /// <param name="value">The value.</param>
    /// <param name="host">The document's fields.</param>
    /// <returns>The text to show.</returns>
    public static string Format(FormScript format, string value, IFormScriptHost host)
    {
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(value);
        return format.Program is { } program && program.Run(host, FormScriptEvent.ForValue(value)) is { Completed: true } outcome
            ? outcome.Value
            : FormScriptEngine.Format(format, value);
    }

    /// <summary>Checks a value the reader typed against the field's keystroke script.</summary>
    /// <param name="keystroke">The keystroke script.</param>
    /// <param name="value">The whole value typed.</param>
    /// <param name="host">The document's fields.</param>
    /// <param name="message">Why the value is refused; empty when it is accepted.</param>
    /// <returns><see langword="true"/> when the value is accepted.</returns>
    public static bool Accepts(FormScript keystroke, string value, IFormScriptHost host, out string message)
    {
        ArgumentNullException.ThrowIfNull(keystroke);
        ArgumentNullException.ThrowIfNull(value);
        message = string.Empty;
        return keystroke.Program is { } program ? Accepted(program.Run(host, new(value, string.Empty, true)), out message) : FormScriptEngine.Accepts(keystroke, value);
    }

    /// <summary>Checks a value against the field's validate script.</summary>
    /// <param name="validate">The validate script.</param>
    /// <param name="value">The value.</param>
    /// <param name="host">The document's fields.</param>
    /// <param name="message">Why the value is refused; empty when it is valid.</param>
    /// <returns><see langword="true"/> when the value is valid.</returns>
    public static bool Validate(FormScript validate, string value, IFormScriptHost host, out string message)
    {
        ArgumentNullException.ThrowIfNull(validate);
        ArgumentNullException.ThrowIfNull(value);
        message = string.Empty;
        return validate.Program is { } program ? Accepted(program.Run(host, new(value, string.Empty, true)), out message) : FormScriptEngine.Validate(validate, value, out message);
    }

    /// <summary>Calculates a field's value from its calculate script.</summary>
    /// <param name="calculate">The calculate script.</param>
    /// <param name="current">The field's value now.</param>
    /// <param name="host">The document's fields.</param>
    /// <param name="text">The new value, before the field's format script.</param>
    /// <returns><see langword="true"/> when the script ran and gave a value.</returns>
    public static bool TryCalculate(FormScript calculate, string current, IFormScriptHost host, out string text)
    {
        ArgumentNullException.ThrowIfNull(calculate);
        ArgumentNullException.ThrowIfNull(current);
        text = current;
        if (calculate.Program is not { } program || program.Run(host, FormScriptEvent.ForValue(current)) is not { Completed: true } outcome)
        {
            return false;
        }

        text = outcome.Value;
        return true;
    }

    /// <summary>Reads what a keystroke or validate program decided.</summary>
    /// <param name="outcome">What the program did.</param>
    /// <param name="message">Why the value is refused; empty when it is accepted.</param>
    /// <returns><see langword="true"/> when the value is accepted.</returns>
    private static bool Accepted(in FormScriptOutcome outcome, out string message)
    {
        var accepted = !outcome.Completed || outcome.Accepted;
        message = outcome.Message.Length > 0 ? outcome.Message : DefaultRefusal;
        message = accepted ? string.Empty : message;
        return accepted;
    }
}
