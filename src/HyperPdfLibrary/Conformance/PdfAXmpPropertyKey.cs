// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>Names an XMP property by namespace and local name.</summary>
/// <param name="Namespace">The namespace URI.</param>
/// <param name="Name">The local name.</param>
internal readonly record struct PdfAXmpPropertyKey(string? Namespace, string Name);
