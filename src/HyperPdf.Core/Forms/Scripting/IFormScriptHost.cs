// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Forms.Scripting;

/// <summary>
/// What a sandboxed form script can reach in the document: other fields' values and looks. It reaches nothing else:
/// no files, no network, no timers, no other apps. Messages the script wants to show go to <see cref="Tell"/> and also come back in the outcome.
/// </summary>
public interface IFormScriptHost
{
    /// <summary>Gets a field's value.</summary>
    /// <param name="fieldName">The field's full name.</param>
    /// <returns>The value, or <see langword="null"/> when the form has no such field.</returns>
    string? GetFieldValue(string fieldName);

    /// <summary>Sets a field's value.</summary>
    /// <param name="fieldName">The field's full name.</param>
    /// <param name="value">The new value.</param>
    void SetFieldValue(string fieldName, string value);

    /// <summary>Shows or hides a field.</summary>
    /// <param name="fieldName">The field's full name.</param>
    /// <param name="display">Where the field shows.</param>
    void SetFieldDisplay(string fieldName, FormFieldDisplay display);

    /// <summary>Makes a field read-only, or lets it be edited again.</summary>
    /// <param name="fieldName">The field's full name.</param>
    /// <param name="readOnly">Whether the field is read-only.</param>
    void SetFieldReadOnly(string fieldName, bool readOnly);

    /// <summary>
    /// Tells the reader something in a calm message: what a script asked to show with <c>app.alert</c>, or why a script was
    /// skipped because it needs more than the sandbox gives.
    /// </summary>
    /// <param name="message">The message.</param>
    void Tell(string message);
}
