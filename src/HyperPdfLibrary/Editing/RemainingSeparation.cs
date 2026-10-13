// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Features;

namespace HyperPdfLibrary.Editing;

/// <summary>A separation page a deletion leaves.</summary>
/// <param name="Page">The page.</param>
/// <param name="Separation">Its separation details.</param>
[DebuggerDisplay("RemainingSeparation: {Separation.PageIndex}")]
internal readonly record struct RemainingSeparation(PdfPage Page, PdfSeparationPage Separation);
