// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Forms.Scripting;

/// <summary>What a form script sees as <c>event</c> when it runs for a field.</summary>
/// <param name="Value">The field's value, <c>event.value</c>.</param>
/// <param name="Change">The text just typed, <c>event.change</c>; empty when not typing.</param>
/// <param name="WillCommit">Whether the value is about to be committed, <c>event.willCommit</c>.</param>
[DebuggerDisplay("FormScriptEvent: {Value}")]
public readonly record struct FormScriptEvent(string Value, string Change, bool WillCommit)
{
    /// <summary>Makes the event of a script that runs on a value and nothing typed.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The event.</returns>
    public static FormScriptEvent ForValue(string value) => new(value, string.Empty, false);
}
