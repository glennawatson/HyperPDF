// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Signing;

/// <summary>One required attribute of a subject distinguished name.</summary>
/// <param name="Key">The attribute's name, such as <c>CN</c>, or its object identifier.</param>
/// <param name="Value">The value it must have.</param>
[DebuggerDisplay("PdfSeedNameAttribute: {Key}={Value}")]
internal readonly record struct PdfSeedNameAttribute(string Key, string Value);
