// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Fonts;

namespace HyperPdfLibrary.Conformance;

/// <summary>An open face a PDF/A conversion embeds in place of a standard font.</summary>
/// <param name="Family">The family.</param>
/// <param name="Bold">Whether the face is bold.</param>
/// <param name="Italic">Whether the face slants.</param>
internal readonly record struct PdfAConversionFace(BundledFamily Family, bool Bold, bool Italic);
