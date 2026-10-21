// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Forms;

namespace HyperPdf.Presentation.Input;

/// <summary>What a key did to the form fields, and the field whose choices to show.</summary>
/// <param name="Outcome">What the key did.</param>
/// <param name="Field">The field whose choices to show for <see cref="FormKeyOutcome.OpenChoices"/>, and otherwise <see langword="null"/>.</param>
[DebuggerDisplay("FormKeyResult: {Outcome}")]
public readonly record struct FormKeyResult(FormKeyOutcome Outcome, FormField? Field)
{
    /// <summary>Gets a value indicating whether a field took the key.</summary>
    public bool IsHandled => Outcome != FormKeyOutcome.Unhandled;
}
