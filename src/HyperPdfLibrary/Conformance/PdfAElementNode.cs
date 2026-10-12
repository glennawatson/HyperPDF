// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>A structure tree node waiting to be visited.</summary>
/// <param name="Element">The node's dictionary.</param>
/// <param name="Depth">Its depth below the root.</param>
internal readonly record struct PdfAElementNode(PdfDictionary Element, int Depth);
