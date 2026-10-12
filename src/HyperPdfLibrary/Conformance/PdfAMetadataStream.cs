// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>A metadata stream found in a document, with where it was found for the report.</summary>
/// <param name="Stream">The stream.</param>
/// <param name="Context">Where it was found, such as "object 12".</param>
internal sealed record PdfAMetadataStream(PdfStream Stream, string Context);
