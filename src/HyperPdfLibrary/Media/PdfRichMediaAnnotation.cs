// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Media;

/// <summary>A rich media annotation.</summary>
/// <param name="Assets">The assets.</param>
/// <param name="Configurations">The configurations.</param>
/// <param name="ActivationCondition">When the content activates: XA (explicitly), PO (page opened) or PV (page visible); null when not set.</param>
/// <param name="DeactivationCondition">When the content deactivates: XD, PC or PI; null when not set.</param>
/// <param name="ViewCount">The number of 3D views in <c>/Views</c>.</param>
[DebuggerDisplay("PdfRichMediaAnnotation: {Assets.Length} assets")]
public sealed record PdfRichMediaAnnotation(
    PdfRichMediaAsset[] Assets,
    PdfRichMediaConfiguration[] Configurations,
    string? ActivationCondition,
    string? DeactivationCondition,
    int ViewCount);
