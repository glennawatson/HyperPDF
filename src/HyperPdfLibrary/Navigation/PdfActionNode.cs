// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Navigation;

/// <summary>An action read as data with its <c>/Next</c> chain; nothing runs.</summary>
/// <param name="Subtype">The <c>/S</c> name, or an empty string.</param>
/// <param name="Action">The action; an <see cref="UnsupportedAction"/> for names the library does not read.</param>
/// <param name="Next">The actions that follow, in order, with their own chains.</param>
/// <param name="Dictionary">The action dictionary.</param>
[DebuggerDisplay("PdfActionNode: {Subtype} then {Next.Length}")]
public sealed record PdfActionNode(string Subtype, PdfAction Action, PdfActionNode[] Next, PdfDictionary Dictionary);
