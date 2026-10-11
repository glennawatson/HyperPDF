// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Rendering;

/// <summary>Settings that change decoded images or recorded content shared by renderers of one document.</summary>
/// <param name="ImageCacheBytes">The decoded image cache limit.</param>
/// <param name="SimulateOverprint">Whether image overprint is simulated.</param>
/// <param name="FormHighlight">The tint drawn over fillable form fields.</param>
internal readonly record struct PdfRenderCacheKey(long ImageCacheBytes, bool SimulateOverprint, PdfFormHighlight FormHighlight);
