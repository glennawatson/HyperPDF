// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Engine.Tests;

/// <summary>The samples each engine drew differently from the encoded image.</summary>
/// <param name="Pdfium">The wrong samples from PDFium.</param>
/// <param name="HyperPdf">The wrong samples from HyperPDF.</param>
internal readonly record struct SampleErrors(int Pdfium, int HyperPdf);
