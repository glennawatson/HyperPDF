// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Editing;

/// <summary>A watermark form in the document.</summary>
/// <param name="Form">The form XObject.</param>
/// <param name="Width">Its width in points.</param>
/// <param name="Height">Its height in points.</param>
internal readonly record struct PdfWatermarkForm(PdfObjectId Form, float Width, float Height);
