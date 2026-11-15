// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Rendering.Vulkan;

/// <summary>Where the backend's finished frames go to be seen: a swapchain of an X11 window, or images shared with a Wayland compositor.</summary>
internal interface IVulkanFrameOutput : IDisposable
{
    /// <summary>Makes the output's images again for a new frame size.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    void Resize(int width, int height);

    /// <summary>Records the copy of the finished frame into the output's next image.</summary>
    /// <param name="commandBuffer">The command buffer, after the render pass has ended.</param>
    /// <param name="frame">The backend's frame image, in the colour attachment layout.</param>
    void RecordCopy(ulong commandBuffer, VulkanFrameTarget frame);

    /// <summary>Shows the image the frame was copied into. The copy must have finished on the GPU.</summary>
    void Present();
}
