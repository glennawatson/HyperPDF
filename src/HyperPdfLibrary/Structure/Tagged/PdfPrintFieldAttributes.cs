// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>Non-inherited non-interactive form attributes from ISO 32000-2, Table 383.</summary>
/// <param name="Role">The field's role name, including unknown names; null when no name is specified.</param>
/// <param name="Checked">The checked-state name, including unknown names; null when omitted, whose standard default is off.</param>
/// <param name="Description">The alternate field name decoded from Desc; null when no text string is specified.</param>
[DebuggerDisplay("PdfPrintFieldAttributes: {Role}, state {Checked}")]
public readonly record struct PdfPrintFieldAttributes(string? Role, string? Checked, string? Description);
