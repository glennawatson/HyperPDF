// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdf.Engine;

/// <summary>A page whose removed annotations were hidden for saving.</summary>
/// <param name="Page">The page.</param>
/// <param name="Annotations">The full annotation array.</param>
internal readonly record struct RemovedAnnotationPage(PdfPage Page, PdfArray Annotations);
