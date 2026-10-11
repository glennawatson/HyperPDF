// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Rendering;

/// <summary>Limits and switches selected by a renderer; equal image and drawing settings share a document cache.</summary>
/// <param name="ImageCacheBytes">
/// The most decoded image pixel bytes renderers with these options keep between page recordings. The default, 64 MiB, holds the decoded
/// images of two full-page 300 dpi greyscale scans; recorded pages keep their own references, so a smaller limit only
/// costs re-decoding when a page is recorded again.
/// </param>
/// <param name="SimulateOverprint">
/// Whether to simulate overprint the way PDFium's source describes it: an opaque, unmasked DeviceCMYK, Separation or
/// DeviceN image drawn with fill overprint on, overprint mode 0, the Normal blend and both alphas at 1 is darkened into
/// the page. Off by default, because the PDFium build the app ships draws such images without darkening.
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

    /// <summary>
    /// Gets the most memory the recorded page pictures may hold together: each picture's own operations plus the pixels of
    /// every image it drew, because a picture keeps its images alive after the image cache lets them go. When the pictures
    /// hold more, the least recently used pages are dropped. The page being drawn is always kept, even when it alone is
    /// over the limit. The default is 128 MiB; a smaller limit costs re-recording pages that scroll back into view.
    /// </summary>
    public long PictureCacheBytes { get; init; } = PictureBytes;
}
