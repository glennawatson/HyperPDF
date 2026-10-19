// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Navigation;

/// <summary>Hides or shows annotations or form fields.</summary>
/// <param name="Targets">The field names or annotation titles to change.</param>
/// <param name="Hide">Whether to hide the targets (true) or show them.</param>
[DebuggerDisplay("HideAction: {Targets.Length} targets, hide={Hide}")]
public sealed record HideAction(string[] Targets, bool Hide);
