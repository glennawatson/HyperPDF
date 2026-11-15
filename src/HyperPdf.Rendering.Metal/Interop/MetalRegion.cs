// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Rendering.Metal.Interop;

/// <summary>MTLRegion, in the field order and layout of the C structure: an origin and a size, each three unsigned integers, which are 64 bits wide on every Mac.</summary>
/// <param name="X">The left edge in pixels.</param>
/// <param name="Y">The top edge in pixels.</param>
/// <param name="Z">The first slice.</param>
/// <param name="Width">The width in pixels.</param>
/// <param name="Height">The height in pixels.</param>
/// <param name="Depth">The number of slices.</param>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct MetalRegion(ulong X, ulong Y, ulong Z, ulong Width, ulong Height, ulong Depth);
