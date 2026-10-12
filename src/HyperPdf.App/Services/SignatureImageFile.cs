// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using HyperPdf.Core.Annotations;

namespace HyperPdf.App.Services;

/// <summary>Reads a picture of a signature (PNG, JPEG, BMP or WebP) into straight-alpha BGRA pixels.</summary>
public static class SignatureImageFile
{
    /// <summary>The widest image kept, in pixels; wider pictures are scaled down as they are read.</summary>
    private const int MaxWidth = 1600;

    /// <summary>The bytes per BGRA pixel.</summary>
    private const int Channels = 4;

    /// <summary>The red channel's byte offset in BGRA.</summary>
    private const int RedOffset = 2;

    /// <summary>The alpha channel's byte offset in BGRA.</summary>
    private const int AlphaOffset = 3;

    /// <summary>A fully opaque alpha value.</summary>
    private const byte Opaque = 255;

    /// <summary>Reads an image file.</summary>
    /// <param name="path">The file.</param>
    /// <returns>The image, or <see langword="null"/> when the file cannot be read as a picture.</returns>
    public static DecodedImage? Load(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        try
        {
            using var bitmap = Decode(path);
            return Read(bitmap, Path.GetFileName(path));
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException)
        {
            // The decoder reports a file that is not a picture with one of these.
            return null;
        }
    }

    /// <summary>Decodes a file, scaling very wide pictures down.</summary>
    /// <param name="path">The file.</param>
    /// <returns>The bitmap.</returns>
    private static WriteableBitmap Decode(string path)
    {
        WriteableBitmap bitmap;
        using (var stream = File.OpenRead(path))
        {
            bitmap = WriteableBitmap.Decode(stream);
        }

        if (bitmap.PixelSize.Width <= MaxWidth)
        {
            return bitmap;
        }

        bitmap.Dispose();
        using var again = File.OpenRead(path);
        return WriteableBitmap.DecodeToWidth(again, MaxWidth);
    }

    /// <summary>Copies a bitmap's pixels as straight-alpha BGRA.</summary>
    /// <param name="bitmap">The bitmap.</param>
    /// <param name="name">The file name.</param>
    /// <returns>The image.</returns>
    private static DecodedImage? Read(WriteableBitmap bitmap, string name)
    {
        using var frame = bitmap.Lock();
        var format = frame.Format;
        if (format != PixelFormat.Bgra8888 && format != PixelFormat.Rgba8888 && format != PixelFormat.Rgb32)
        {
            return null;
        }

        var width = frame.Size.Width;
        var height = frame.Size.Height;
        var stride = width * Channels;
        var pixels = new byte[stride * height];
        for (var row = 0; row < height; row++)
        {
            Marshal.Copy(frame.Address + (row * frame.RowBytes), pixels, row * stride, stride);
        }

        if (format != PixelFormat.Bgra8888)
        {
            // RGBA and RGBX hold red first; RGBX's fourth byte is padding, so those pixels are opaque.
            var opaque = format == PixelFormat.Rgb32;
            for (var offset = 0; offset < pixels.Length; offset += Channels)
            {
                (pixels[offset], pixels[offset + RedOffset]) = (pixels[offset + RedOffset], pixels[offset]);
                if (opaque)
                {
                    pixels[offset + AlphaOffset] = Opaque;
                }
            }
        }

        if (frame.AlphaFormat == AlphaFormat.Premul)
        {
            SignaturePixels.Unpremultiply(pixels);
        }

        return new(name, pixels, width, height);
    }
}
