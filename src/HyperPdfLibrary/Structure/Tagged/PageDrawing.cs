// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>What a page draws besides text, and its size, as the layout analysis needs it.</summary>
/// <param name="Images">The images in drawing order.</param>
/// <param name="Rulings">The straight horizontal and vertical lines.</param>
/// <param name="Width">The displayed width in points.</param>
/// <param name="Height">The displayed height in points.</param>
[DebuggerDisplay("PageDrawing: {Images.Length} images, {Rulings.Length} rulings")]
internal readonly record struct PageDrawing(PdfMarkedImage[] Images, LayoutRuling[] Rulings, float Width, float Height);
