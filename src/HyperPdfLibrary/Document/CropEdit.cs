// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Document;

/// <summary>A crop to apply to pages.</summary>
/// <param name="Document">The document.</param>
/// <param name="Pages">The zero based page indexes.</param>
/// <param name="Margins">What is taken off each side.</param>
[DebuggerDisplay("CropEdit: {Pages.Length} pages")]
internal sealed record CropEdit(PdfDocument Document, int[] Pages, CropMargins Margins);
