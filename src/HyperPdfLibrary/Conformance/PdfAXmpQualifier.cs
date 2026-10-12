// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>A qualifier of an XMP node, such as <c>xml:lang</c>.</summary>
/// <param name="Name">The qualifier's name as written, with its prefix.</param>
/// <param name="Value">The value.</param>
internal sealed record PdfAXmpQualifier(string Name, string Value);
