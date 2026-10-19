// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Fonts;

namespace HyperPdfLibrary.Rendering;

/// <summary>A Type 3 glyph whose procedure is running.</summary>
/// <param name="Font">The Type 3 font.</param>
/// <param name="Code">The character code.</param>
/// <param name="Images">The image masks the procedure has drawn so far.</param>
internal readonly record struct Type3Scope(PdfFont Font, int Code, int Images);
