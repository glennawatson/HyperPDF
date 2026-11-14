// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Rendering.Metal.Interop;

/// <summary>MTLClearColor, in the field order and layout of the C structure.</summary>
/// <param name="Red">The red, already multiplied by alpha.</param>
/// <param name="Green">The green, already multiplied by alpha.</param>
/// <param name="Blue">The blue, already multiplied by alpha.</param>
/// <param name="Alpha">The alpha.</param>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct MetalClearColor(double Red, double Green, double Blue, double Alpha);
