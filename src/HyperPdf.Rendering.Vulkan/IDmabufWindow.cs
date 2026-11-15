// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Rendering.Vulkan;

/// <summary>
/// A window that shows frames the GPU shares as dmabufs, which is how a Wayland compositor takes them without a copy. The images are
/// 32-bit with blue in the lowest byte and no alpha in use, in linear layout.
/// </summary>
public interface IDmabufWindow
{
    /// <summary>Takes the images frames will be drawn into, in place of any taken before. The window owns the descriptors and closes them.</summary>
    /// <param name="frames">The images.</param>
    /// <returns><see langword="true"/> when the window system accepted every image.</returns>
    bool SetFrames(ReadOnlySpan<DmabufFrame> frames);

    /// <summary>Finds an image the window system is not showing or reading.</summary>
    /// <returns>The image's place in the list given to <see cref="SetFrames"/>, or -1 when every image is in use and the frame is skipped.</returns>
    int TakeFreeFrame();

    /// <summary>Shows an image. The GPU has finished drawing into it.</summary>
    /// <param name="index">The image's place in the list.</param>
    void ShowFrame(int index);
}
