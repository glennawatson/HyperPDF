// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdf.Engine;

/// <summary>A page form and its size on an output sheet.</summary>
/// <param name="Form">The page form object.</param>
/// <param name="Width">The page width in points.</param>
/// <param name="Height">The page height in points.</param>
internal readonly record struct SheetPage(PdfObjectId Form, float Width, float Height);
