// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Drawing;

namespace HyperPdfLibrary.Rendering;

/// <summary>
/// Lists the images a recording draws. A recorded picture keeps a native reference to each image it drew, so the images
/// stay alive for as long as the picture does, whether or not the image cache still holds them. Each distinct image is
/// listed once; the renderer charges an image shared by several pictures once, not once per picture. One recording thread
/// uses an instance, so it is not thread-safe.
/// </summary>
[DebuggerDisplay("PictureWeight: {_images.Count} images")]
public sealed class PictureWeight
{
    /// <summary>The unique ids of the images already listed.</summary>
    private readonly HashSet<uint> _seen = [];

    /// <summary>The distinct images drawn, in the order they were first drawn.</summary>
    private readonly List<ImageWeight> _images = [];

    /// <summary>Lists an image the first time it is drawn.</summary>
    /// <param name="image">The image.</param>
    public void Add(IPdfRenderImage image)
    {
        if (_seen.Add(image.ImageId))
        {
            _images.Add(new(image.ImageId, image.PixelBytes));
        }
    }

    /// <summary>Gets the distinct images drawn so far.</summary>
    /// <returns>A copy of the list; empty when no image was drawn.</returns>
    public ImageWeight[] ToArray() => _images.Count == 0 ? [] : [.. _images];

    /// <summary>Forgets every image listed, so the instance can count another recording.</summary>
    public void Reset()
    {
        _seen.Clear();
        _images.Clear();
    }
}
