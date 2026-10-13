// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Engine.Tests.PdfiumReference;

/// <summary>A rectangle in PDF page space, laid out as PDFium's <c>FS_RECTF</c>.</summary>
/// <param name="Left">The left edge.</param>
/// <param name="Top">The top edge.</param>
/// <param name="Right">The right edge.</param>
/// <param name="Bottom">The bottom edge.</param>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct PdfiumRect(float Left, float Top, float Right, float Bottom);
