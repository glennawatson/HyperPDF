// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Engine.Tests.PdfiumReference;

/// <summary>An <c>/EmbeddedFiles</c> entry as PDFium reads it.</summary>
/// <param name="Index">The entry's place in the name tree.</param>
/// <param name="Name">The name PDFium gives the file.</param>
/// <param name="Data">The decoded file bytes, or <see langword="null"/> when PDFium finds no file.</param>
internal sealed record PdfiumAttachment(int Index, string Name, byte[]? Data);
