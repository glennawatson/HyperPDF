// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Rendering;
using HyperPdf.ViewModels.Sidebar;

namespace HyperPdf.Viewer.Chrome.Forms;

/// <summary>
/// Turns the latest picture of a film into a surface the window can draw. The surface is made again only when the film's size changes,
/// and filled again only when there is a new picture.
/// </summary>
internal static class MediaFrames
{
    /// <summary>The bytes of each pixel: blue, green, red and alpha.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The number of a picture that has not been shown.</summary>
    private const int NoPicture = -1;

    /// <summary>Brings the surface up to date with the player.</summary>
    /// <param name="surfaces">Makes the surfaces.</param>
    /// <param name="frame">The surface as it is now.</param>
    /// <param name="player">The player.</param>
    /// <returns>The surface as it is after, which is <paramref name="frame"/> when nothing changed.</returns>
    internal static MediaFrame Get(IRenderSurfaceFactory surfaces, MediaFrame frame, MediaPlayerViewModel player)
    {
        var size = player.FrameSize;
        if (size.Width <= 0 || size.Height <= 0)
        {
            return frame;
        }

        var current = frame;
        if (frame.Surface is null || size.Width != frame.Width || size.Height != frame.Height)
        {
            frame.Surface?.Dispose();
            current = new(surfaces.Create(size.Width, size.Height), size.Width, size.Height, NoPicture);
        }

        if (current.Version == player.FrameVersion || current.Surface is not { } surface)
        {
            return current;
        }

        return surface.Write(player, static (RenderTarget target, in MediaPlayerViewModel shown) => Copy(shown, target)) ? current with { Version = player.FrameVersion } : current;
    }

    /// <summary>Lets go of the surface.</summary>
    /// <param name="frame">The surface.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Release(MediaFrame frame) => frame.Surface?.Dispose();

    /// <summary>Copies the latest picture into the pixels of the surface, row by row because the surface may pad its rows.</summary>
    /// <param name="player">The player.</param>
    /// <param name="target">The pixels to fill.</param>
    /// <returns><see langword="true"/> when a picture was copied.</returns>
    private static bool Copy(MediaPlayerViewModel player, RenderTarget target)
    {
        var row = target.Width * BytesPerPixel;
        var buffer = ArrayPool<byte>.Shared.Rent(row * target.Height);
        try
        {
            if (!player.TryCopyFrame(buffer))
            {
                return false;
            }

            for (var y = 0; y < target.Height; y++)
            {
                buffer.AsSpan(y * row, row).CopyTo(target.Pixels.Slice(y * target.Stride, row));
            }

            return true;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
