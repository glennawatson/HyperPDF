// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;

namespace HyperPdf.ViewModels.Output;

/// <summary>One sheet of a print preview: a page of the file that will print.</summary>
/// <param name="Document">The preview document.</param>
/// <param name="PageIndex">The page in the preview document.</param>
/// <param name="Size">The page size in points.</param>
/// <param name="Caption">The caption under the sheet, such as "2 of 5".</param>
[DebuggerDisplay("PrintPreviewPage: {Caption}")]
public sealed record PrintPreviewPage(IDocument Document, int PageIndex, PageSize Size, string Caption);
