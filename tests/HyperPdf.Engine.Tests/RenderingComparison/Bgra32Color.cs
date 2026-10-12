// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Engine.Tests.RenderingComparison;

/// <summary>A premultiplied BGRA32 pixel.</summary>
/// <param name="Blue">The blue channel.</param>
/// <param name="Green">The green channel.</param>
/// <param name="Red">The red channel.</param>
/// <param name="Alpha">The alpha channel.</param>
internal readonly record struct Bgra32Color(byte Blue, byte Green, byte Red, byte Alpha);
