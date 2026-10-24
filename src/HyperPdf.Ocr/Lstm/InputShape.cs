// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Ocr.Lstm;

/// <summary>The shape of picture a model takes. A zero says the size is whatever the picture is.</summary>
/// <param name="Batch">The number of pictures at once.</param>
/// <param name="Height">The height in pixels.</param>
/// <param name="Width">The width in pixels.</param>
/// <param name="Depth">The number of channels.</param>
/// <param name="LossType">How the model's output is read.</param>
[DebuggerDisplay("InputShape: {Height} high, {Width} wide, {Depth} channels")]
public readonly record struct InputShape(int Batch, int Height, int Width, int Depth, int LossType);
