// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>Where a page is and what it inherits, kept until the page itself is first needed.</summary>
/// <param name="Id">The page object's id, or an invalid id for a direct page dictionary.</param>
/// <param name="Direct">The page dictionary when the page is direct; <see langword="null"/> for an indirect page.</param>
/// <param name="InheritIndex">The position of the attributes the page inherits in the page set's table of them.</param>
[DebuggerDisplay("PdfPageEntry: {Id}")]
internal readonly record struct PdfPageEntry(PdfObjectId Id, PdfDictionary? Direct, int InheritIndex);
