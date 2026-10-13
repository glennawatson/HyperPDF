// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Rendering;

/// <summary>Identifies a recorded soft mask: its dictionary, the transform it was recorded with and what it was recorded under.</summary>
/// <param name="Mask">The soft mask dictionary.</param>
/// <param name="Ctm">The matrix from user space to the page when the mask was set.</param>
/// <param name="Resources">The resources the mask group inherits when it has none of its own, or null.</param>
/// <param name="Mode">The layer version, layer zoom class and print mode the mask was recorded under.</param>
[DebuggerDisplay("SoftMaskKey: {Ctm}")]
internal readonly record struct SoftMaskKey(PdfDictionary Mask, Matrix3x2 Ctm, PdfDictionary? Resources, RecordingMode Mode);
