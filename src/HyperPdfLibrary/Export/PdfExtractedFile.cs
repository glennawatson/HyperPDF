// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Export;

/// <summary>A file taken out of a document: an image a page draws, or a font program a page uses.</summary>
/// <param name="PageIndex">The zero-based index of the page the file was found on first.</param>
/// <param name="Name">A file name that suggests the page and the kind of file, with its extension, such as <c>page-1-image-2.png</c>.</param>
/// <param name="Kind">Whether the file is an image or a font.</param>
/// <param name="Data">The file's bytes.</param>
[DebuggerDisplay("PdfExtractedFile: {Name}, {Data.Length} bytes")]
public sealed record PdfExtractedFile(int PageIndex, string Name, PdfExtractedFileKind Kind, byte[] Data);
