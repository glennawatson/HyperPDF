// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Forms;

/// <summary>Where a widget sits: its page and its position in the page's <c>/Annots</c> array.</summary>
/// <param name="PageIndex">The zero based page index.</param>
/// <param name="Index">The widget's index in the page's annotations, as <see cref="PdfFormWidget.Index"/> reports it.</param>
[DebuggerDisplay("PdfWidgetLocation: page {PageIndex} index {Index}")]
public readonly record struct PdfWidgetLocation(int PageIndex, int Index);
