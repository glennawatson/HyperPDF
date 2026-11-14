// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Rendering.Metal.Interop;

/// <summary>CGSize, in the field order and layout of the C structure.</summary>
/// <param name="Width">The width.</param>
/// <param name="Height">The height.</param>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct MetalSize(double Width, double Height);
