// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Rendering.Direct3D12.Interop;

namespace HyperPdf.Rendering.Direct3D12;

/// <summary>Records the copy of a tile's pixels from the upload buffer into its texture.</summary>
internal static unsafe class Direct3D12Textures
{
    /// <summary>The DXGI format of 8 bit BGRA.</summary>
    private const uint FormatBgra = 87;

    /// <summary>The bytes per pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The row pitch Direct3D 12 requires of a texture copy, in bytes.</summary>
    private const int PitchAlignment = 256;

    /// <summary>The offset alignment Direct3D 12 requires of a texture copy in a buffer, in bytes.</summary>
    private const int PlacementAlignment = 512;

    /// <summary>The texture copy location type of a subresource index.</summary>
    private const uint CopySubresource = 0;

    /// <summary>The texture copy location type of a placed footprint in a buffer.</summary>
    private const uint CopyFootprint = 1;

    /// <summary>Gets the bytes a texture's pixels take in the upload buffer, with the alignment Direct3D 12 asks of a copy.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <returns>The size in bytes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int UploadSize(int width, int height) => Direct3D12Device.AlignUp(RowPitch(width) * height, PlacementAlignment);

    /// <summary>Copies pixels into the upload buffer and records the copy into the texture, between the barriers that allow it.</summary>
    /// <param name="device">The device whose list records the copy.</param>
    /// <param name="upload">The upload buffer.</param>
    /// <param name="offset">Where the pixels go in the upload buffer. It must be a multiple of the placement alignment, which <see cref="UploadSize"/> keeps.</param>
    /// <param name="target">The texture and the shape of the pixels.</param>
    /// <param name="pixels">The pixels.</param>
    internal static void Copy(Direct3D12Device device, Direct3D12UploadBuffer upload, int offset, in Direct3D12TextureCopy target, ReadOnlySpan<byte> pixels)
    {
        var pitch = RowPitch(target.Width);
        var rowBytes = target.Width * BytesPerPixel;
        for (var row = 0; row < target.Height; row++)
        {
            pixels.Slice(row * target.Stride, rowBytes).CopyTo(new(upload.Mapped + offset + (row * pitch), rowBytes));
        }

        if (!target.IsNew)
        {
            device.Transition(target.Texture, Direct3D12Device.StatePixelResource, Direct3D12Device.StateCopyDestination);
        }

        var destination = new TextureCopyLocation(target.Texture, CopySubresource, 0, 0, 0, 0, 0, 0);
        var source = new TextureCopyLocation((ulong)upload.Resource, CopyFootprint, (ulong)offset, FormatBgra, (uint)target.Width, (uint)target.Height, 1, (uint)pitch);
        CommandListCalls.CopyTexture(device.List, &destination, &source);
        device.Transition(target.Texture, Direct3D12Device.StateCopyDestination, Direct3D12Device.StatePixelResource);
    }

    /// <summary>Gets the bytes per row of a texture's pixels in the upload buffer.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <returns>The pitch in bytes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int RowPitch(int width) => Direct3D12Device.AlignUp(width * BytesPerPixel, PitchAlignment);
}
