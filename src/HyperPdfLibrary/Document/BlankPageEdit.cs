// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Document;

/// <summary>The blank page one edit adds: where it goes and how big it is.</summary>
/// <param name="Document">The document.</param>
/// <param name="Index">The index the page takes, from 0 to the page count.</param>
/// <param name="Width">The width of the page in points.</param>
/// <param name="Height">The height of the page in points.</param>
[DebuggerDisplay("BlankPageEdit: at {Index}, {Width} x {Height}")]
internal sealed record BlankPageEdit(PdfDocument Document, int Index, float Width, float Height);
