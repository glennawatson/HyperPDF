// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>A content stream waiting to be read: its parts, read as one, and the resources its names refer to.</summary>
/// <param name="Parts">The stream or streams.</param>
/// <param name="Resources">The resource dictionary, or <see langword="null"/>.</param>
internal sealed record PdfASyntaxWork(PdfStream[] Parts, PdfDictionary? Resources);
