// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Diagnostics;

namespace HyperPdfLibrary.Rendering;

/// <summary>Limits and switches selected by a renderer; equal image and drawing settings share a document cache.</summary>
/// <param name="ImageCacheBytes">
/// The most decoded image pixel bytes renderers with these options keep between page recordings. The default, 64 MiB, holds the decoded
/// images of two full-page 300 dpi greyscale scans; recorded pages keep their own references, so a smaller limit only
/// costs re-decoding when a page is recorded again.
/// </param>
/// <param name="SimulateOverprint">
/// Whether to preview overprint (ISO 32000-2 8.6.7 and 11.7.4). Fills, strokes, text, stencil masks and opaque images
/// that leave some backdrop colorants alone (a Separation, a DeviceN space that is not all four process colorants, or
/// DeviceCMYK in overprint mode 1 with a zero component) are painted with the Multiply blend, which keeps the backdrop's
/// share of the colorants they do not specify. A screen shows the page as RGB, so this is an approximation of the
/// separations a press would print; it is off by default. Objects that specify every colorant replace the backdrop with
/// or without the preview.
/// </param>
[DebuggerDisplay("PdfRenderOptions: images {ImageCacheBytes} bytes, pictures {PictureCacheBytes} bytes, overprint {SimulateOverprint}")]
public sealed record PdfRenderOptions(long ImageCacheBytes, bool SimulateOverprint)
{
    /// <summary>The default limit of the page picture cache: 128 MiB.</summary>
    private const long PictureBytes = 128L * 1024 * 1024;

    /// <summary>Gets the default limit of the image cache: 64 MiB.</summary>
    public static long DefaultImageCacheBytes => ImageCache.DefaultCapacity;

    /// <summary>Gets the default limit of the page picture cache: 128 MiB.</summary>
    public static long DefaultPictureCacheBytes => PictureBytes;

    /// <summary>Gets the default options.</summary>
    public static PdfRenderOptions Default { get; } = new(ImageCache.DefaultCapacity, false);

    /// <summary>Gets the tint drawn over fillable form fields when annotations are drawn; none by default.</summary>
    public PdfFormHighlight FormHighlight { get; init; }

    /// <summary>Gets how edges, thin lines and scaled images are drawn; smooth edges by default.</summary>
    public PdfRenderQuality Quality { get; init; } = PdfRenderQuality.Default;

    /// <summary>
    /// Gets the most memory the recorded page pictures may hold together: each picture's own operations plus the pixels of
    /// every image it drew, because a picture keeps its images alive after the image cache lets them go. When the pictures
    /// hold more, the least recently used pages are dropped. The page being drawn is always kept, even when it alone is
    /// over the limit. The default is 128 MiB; a smaller limit costs re-recording pages that scroll back into view.
    /// </summary>
    public long PictureCacheBytes { get; init; } = PictureBytes;

    /// <summary>
    /// Gets the colorants pages leave out, for a separation view. A name is a Separation or DeviceN colorant, or a process
    /// colorant: Cyan, Magenta, Yellow or Black (DeviceGray and one-component ICC colours paint Black). Objects painted
    /// only in a hidden Separation colorant, and images in such a space, disappear; a hidden process or DeviceN colorant is
    /// cleared from each fill, stroke and glyph colour. An image or shading in a space that still paints another colorant
    /// is drawn whole, because its pixels are not split by colorant. None by default.
    /// </summary>
    public ImmutableArray<string> HiddenColorants { get; init; } = [];
}
