// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Forms.Scripting;

/// <summary>What a sandboxed form script did.</summary>
/// <param name="Completed">Whether the script ran to its end. A script that used something the sandbox refuses, or ran too long, did not.</param>
/// <param name="Value">The value the script left in <c>event.value</c>; the value it was given when it did not complete.</param>
/// <param name="Accepted">Whether the script accepted the value, that is <c>event.rc</c>; true when it did not complete.</param>
/// <param name="Messages">The messages the script asked to show with <c>app.alert</c>, then the reason it did not complete, if any.</param>
[DebuggerDisplay("FormScriptOutcome: Completed={Completed} Accepted={Accepted}")]
public sealed record FormScriptOutcome(bool Completed, string Value, bool Accepted, IReadOnlyList<string> Messages)
{
    /// <summary>Gets the first message, or an empty string when there is none.</summary>
    public string Message => Messages.Count > 0 ? Messages[0] : string.Empty;
}
