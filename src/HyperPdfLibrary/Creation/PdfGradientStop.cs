// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Creation;

/// <summary>One colour of a gradient and where it sits along the gradient.</summary>
/// <param name="Offset">The position from 0 (the start) to 1 (the end).</param>
/// <param name="Red">The red level from 0 to 1.</param>
/// <param name="Green">The green level from 0 to 1.</param>
/// <param name="Blue">The blue level from 0 to 1.</param>
[DebuggerDisplay("PdfGradientStop: {Offset} rgb({Red}, {Green}, {Blue})")]
public readonly record struct PdfGradientStop(float Offset, float Red, float Green, float Blue);
