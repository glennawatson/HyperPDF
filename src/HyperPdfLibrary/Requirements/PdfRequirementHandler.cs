// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Requirements;

/// <summary>
/// An alternative requirement handler a requirement dictionary names in <c>/RH</c> (ISO 32000-2 §12.11.5, Table 276). It
/// is data only: a handler's script is never run.
/// </summary>
/// <param name="Type">The handler type: <c>JS</c> for an ECMAScript handler or <c>NoOp</c>. Any other spelling is not recognised.</param>
/// <param name="ScriptName">The name of the document-level script a <c>JS</c> handler points to, or <see langword="null"/>.</param>
/// <param name="IsRecognized">Whether <paramref name="Type"/> is <c>JS</c> or <c>NoOp</c>; the standard says to ignore any other.</param>
[DebuggerDisplay("PdfRequirementHandler: {Type} {ScriptName}")]
public sealed record PdfRequirementHandler(string Type, string? ScriptName, bool IsRecognized);
