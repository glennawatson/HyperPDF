// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>A file attachment annotation: an icon on a page that holds an embedded file.</summary>
/// <param name="PageIndex">The zero based index of the page.</param>
/// <param name="Id">The annotation's object.</param>
/// <param name="FileName">The attached file's name, or <see langword="null"/> when the annotation has no usable file specification.</param>
/// <param name="Icon">The icon name the annotation sets, such as <c>PushPin</c>; the standard's default when it sets none.</param>
/// <param name="Contents">The annotation's text, or <see langword="null"/> for none.</param>
/// <param name="FileSpecification">The object of the file specification, or an invalid id when it is stored inside the annotation.</param>
[DebuggerDisplay("PdfFileAttachmentAnnotation: page {PageIndex} {FileName}")]
public sealed record PdfFileAttachmentAnnotation(int PageIndex, PdfObjectId Id, string? FileName, string Icon, string? Contents, PdfObjectId FileSpecification);
