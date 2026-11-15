// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Documents;

namespace HyperPdf.Viewer.Chrome.Forms;

/// <summary>One sheet of a print preview to draw: the preview document and how its page is drawn.</summary>
/// <param name="Document">The preview document.</param>
/// <param name="Info">The page, its scale and what is drawn with it.</param>
[DebuggerDisplay("SheetJob: page {Info.PageIndex}")]
internal readonly record struct SheetJob(IDocument Document, PageRenderInfo Info);
