// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Rendering.Direct3D.Interop;

/// <summary>D3D11_TEXTURE2D_DESC, in the field order and layout of the C structure.</summary>
/// <param name="Width">The width in pixels.</param>
/// <param name="Height">The height in pixels.</param>
/// <param name="MipLevels">The number of mip levels.</param>
/// <param name="ArraySize">The number of array slices.</param>
/// <param name="Format">The pixel format.</param>
/// <param name="SampleCount">The samples per pixel.</param>
/// <param name="SampleQuality">The sample quality.</param>
/// <param name="Usage">How the texture is read and written.</param>
/// <param name="BindFlags">How the texture is bound to the pipeline.</param>
/// <param name="CpuAccessFlags">How the CPU may access the texture.</param>
/// <param name="MiscFlags">Other flags.</param>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct Texture2DDescription(
    uint Width,
    uint Height,
    uint MipLevels,
    uint ArraySize,
    uint Format,
    uint SampleCount,
    uint SampleQuality,
    uint Usage,
    uint BindFlags,
    uint CpuAccessFlags,
    uint MiscFlags);
