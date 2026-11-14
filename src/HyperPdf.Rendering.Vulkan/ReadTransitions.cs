// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Rendering.Vulkan.Interop;

namespace HyperPdf.Rendering.Vulkan;

/// <summary>The layout changes that bracket reading the frame image back to the CPU.</summary>
internal static class ReadTransitions
{
    /// <summary>Gets the change from drawing to being copied out.</summary>
    internal static ImageTransition ToRead { get; } = new(
        VkEnums.LayoutColorAttachment,
        VkEnums.LayoutTransferSource,
        VkEnums.AccessColorAttachmentWrite,
        VkEnums.AccessTransferRead,
        VkEnums.StageColorAttachmentOutput,
        VkEnums.StageTransfer);

    /// <summary>Gets the change from being copied out back to drawing.</summary>
    internal static ImageTransition ToDraw { get; } = new(
        VkEnums.LayoutTransferSource,
        VkEnums.LayoutColorAttachment,
        VkEnums.AccessTransferRead,
        VkEnums.AccessColorAttachmentWrite,
        VkEnums.StageTransfer,
        VkEnums.StageColorAttachmentOutput);
}
