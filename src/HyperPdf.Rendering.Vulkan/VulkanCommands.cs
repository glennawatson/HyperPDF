// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Rendering.Vulkan.Interop;

namespace HyperPdf.Rendering.Vulkan;

/// <summary>Records the commands of a frame into a command buffer.</summary>
internal static unsafe class VulkanCommands
{
    /// <summary>A size that holds any structure built here.</summary>
    private const int StructSize = 96;

    /// <summary>The depth range end of a viewport.</summary>
    private const float MaxDepth = 1F;

    /// <summary>Records a barrier that moves an image between layouts.</summary>
    /// <param name="commandBuffer">The command buffer.</param>
    /// <param name="image">The image.</param>
    /// <param name="transition">What changes.</param>
    internal static void Barrier(ulong commandBuffer, ulong image, in ImageTransition transition)
    {
        byte* barrier = stackalloc byte[StructSize];
        var writer = new VkWriter(new(barrier, StructSize));
        writer.U32(VkEnums.ImageMemoryBarrierType);
        writer.U64(0);
        writer.U32(transition.SourceAccess);
        writer.U32(transition.DestinationAccess);
        writer.U32(transition.OldLayout);
        writer.U32(transition.NewLayout);
        writer.U32(VkEnums.QueueFamilyIgnored);
        writer.U32(VkEnums.QueueFamilyIgnored);
        writer.U64(image);
        writer.U32(VkEnums.AspectColor);
        writer.U32(0);
        writer.U32(1);
        writer.U32(0);
        writer.U32(1);
        NativeMethods.CmdPipelineBarrier(commandBuffer, transition.SourceStage, transition.DestinationStage, 0, 0, null, 0, null, 1, barrier);
    }

    /// <summary>Records a copy from a buffer into an image that is in the transfer destination layout.</summary>
    /// <param name="commandBuffer">The command buffer.</param>
    /// <param name="buffer">The source buffer.</param>
    /// <param name="bufferOffset">The offset of the pixels in the buffer.</param>
    /// <param name="image">The destination image.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    internal static void CopyBufferToImage(ulong commandBuffer, ulong buffer, ulong bufferOffset, ulong image, int width, int height)
    {
        byte* region = stackalloc byte[StructSize];
        WholeImageCopy(region, bufferOffset, width, height);
        NativeMethods.CmdCopyBufferToImage(commandBuffer, buffer, image, VkEnums.LayoutTransferDestination, 1, region);
    }

    /// <summary>Records a copy from an image in the transfer source layout into a buffer.</summary>
    /// <param name="commandBuffer">The command buffer.</param>
    /// <param name="image">The source image.</param>
    /// <param name="buffer">The destination buffer.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    internal static void CopyImageToBuffer(ulong commandBuffer, ulong image, ulong buffer, int width, int height)
    {
        byte* region = stackalloc byte[StructSize];
        WholeImageCopy(region, 0, width, height);
        NativeMethods.CmdCopyImageToBuffer(commandBuffer, image, VkEnums.LayoutTransferSource, buffer, 1, region);
    }

    /// <summary>Records a copy of a whole image into another whole image, scaled when their sizes differ and converted when their formats differ.</summary>
    /// <param name="commandBuffer">The command buffer.</param>
    /// <param name="source">The source image, in the transfer source layout.</param>
    /// <param name="destination">The destination image, in the transfer destination layout.</param>
    /// <param name="size">The sizes of the two images.</param>
    internal static void Blit(ulong commandBuffer, ulong source, ulong destination, in BlitSize size)
    {
        byte* region = stackalloc byte[StructSize];
        var writer = new VkWriter(new(region, StructSize));
        WriteWholeImage(ref writer, size.SourceWidth, size.SourceHeight);
        WriteWholeImage(ref writer, size.TargetWidth, size.TargetHeight);
        NativeMethods.CmdBlitImage(commandBuffer, source, VkEnums.LayoutTransferSource, destination, VkEnums.LayoutTransferDestination, 1, region, VkEnums.FilterNearest);
    }

    /// <summary>Begins the frame's render pass and sets the viewport and scissor to the whole frame.</summary>
    /// <param name="commandBuffer">The command buffer.</param>
    /// <param name="renderPass">The render pass.</param>
    /// <param name="framebuffer">The framebuffer.</param>
    /// <param name="width">The frame width.</param>
    /// <param name="height">The frame height.</param>
    internal static void BeginFrame(ulong commandBuffer, ulong renderPass, ulong framebuffer, int width, int height)
    {
        byte* begin = stackalloc byte[StructSize];
        var writer = new VkWriter(new(begin, StructSize));
        writer.U32(VkEnums.RenderPassBeginInfoType);
        writer.U64(0);
        writer.U64(renderPass);
        writer.U64(framebuffer);
        writer.I32(0);
        writer.I32(0);
        writer.U32((uint)width);
        writer.U32((uint)height);
        writer.U32(0);
        writer.U64(0);
        NativeMethods.CmdBeginRenderPass(commandBuffer, begin, VkEnums.SubpassInline);
        SetViewport(commandBuffer, width, height);
    }

    /// <summary>Clears the whole frame to a premultiplied colour inside the render pass.</summary>
    /// <param name="commandBuffer">The command buffer.</param>
    /// <param name="width">The frame width.</param>
    /// <param name="height">The frame height.</param>
    /// <param name="color">The premultiplied colour.</param>
    internal static void ClearFrame(ulong commandBuffer, int width, int height, in VulkanColor color)
    {
        byte* attachment = stackalloc byte[StructSize];
        var attachmentWriter = new VkWriter(new(attachment, StructSize));
        attachmentWriter.U32(VkEnums.AspectColor);
        attachmentWriter.U32(0);
        attachmentWriter.F32(color.Red);
        attachmentWriter.F32(color.Green);
        attachmentWriter.F32(color.Blue);
        attachmentWriter.F32(color.Alpha);

        byte* rect = stackalloc byte[StructSize];
        var rectWriter = new VkWriter(new(rect, StructSize));
        rectWriter.I32(0);
        rectWriter.I32(0);
        rectWriter.U32((uint)width);
        rectWriter.U32((uint)height);
        rectWriter.U32(0);
        rectWriter.U32(1);
        NativeMethods.CmdClearAttachments(commandBuffer, 1, attachment, 1, rect);
    }

    /// <summary>Draws one quad: binds the pipeline, pushes the rectangle and colour, and draws the strip.</summary>
    /// <param name="commandBuffer">The command buffer.</param>
    /// <param name="layout">The pipeline layout.</param>
    /// <param name="push">The values for the push constants.</param>
    internal static void DrawQuad(ulong commandBuffer, ulong layout, in QuadPush push)
    {
        // The block is written as floats in shader order: the rectangle, the frame size, two unused floats and the colour.
        ReadOnlySpan<float> values =
        [
            push.X, push.Y, push.Width, push.Height,
            push.ViewportWidth, push.ViewportHeight, 0, 0,
            push.Color.Red, push.Color.Green, push.Color.Blue, push.Color.Alpha
        ];
        fixed (float* block = values)
        {
            NativeMethods.CmdPushConstants(commandBuffer, layout, VkEnums.PushConstantStages, 0, VkEnums.PushConstantSize, (byte*)block);
        }

        NativeMethods.CmdDraw(commandBuffer, VkEnums.QuadVertexCount, 1, 0, 0);
    }

    /// <summary>Builds a VkBufferImageCopy for a whole colour image.</summary>
    /// <param name="target">The 96 byte buffer.</param>
    /// <param name="bufferOffset">The offset in the buffer.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    private static void WholeImageCopy(byte* target, ulong bufferOffset, int width, int height)
    {
        var writer = new VkWriter(new(target, StructSize));
        writer.U64(bufferOffset);
        writer.U32(0);
        writer.U32(0);
        writer.U32(VkEnums.AspectColor);
        writer.U32(0);
        writer.U32(0);
        writer.U32(1);
        writer.I32(0);
        writer.I32(0);
        writer.I32(0);
        writer.U32((uint)width);
        writer.U32((uint)height);
        writer.U32(1);
    }

    /// <summary>Writes one side of a VkImageBlit: the colour layer of an image and the two corners of its whole area.</summary>
    /// <param name="writer">The writer of the region.</param>
    /// <param name="width">The image width.</param>
    /// <param name="height">The image height.</param>
    private static void WriteWholeImage(ref VkWriter writer, int width, int height)
    {
        writer.U32(VkEnums.AspectColor);
        writer.U32(0);
        writer.U32(0);
        writer.U32(1);
        writer.I32(0);
        writer.I32(0);
        writer.I32(0);
        writer.I32(width);
        writer.I32(height);
        writer.I32(1);
    }

    /// <summary>Sets the dynamic viewport and scissor to the whole frame.</summary>
    /// <param name="commandBuffer">The command buffer.</param>
    /// <param name="width">The frame width.</param>
    /// <param name="height">The frame height.</param>
    private static void SetViewport(ulong commandBuffer, int width, int height)
    {
        byte* viewport = stackalloc byte[StructSize];
        var viewportWriter = new VkWriter(new(viewport, StructSize));
        viewportWriter.F32(0);
        viewportWriter.F32(0);
        viewportWriter.F32(width);
        viewportWriter.F32(height);
        viewportWriter.F32(0);
        viewportWriter.F32(MaxDepth);
        NativeMethods.CmdSetViewport(commandBuffer, 0, 1, viewport);

        byte* scissor = stackalloc byte[StructSize];
        var scissorWriter = new VkWriter(new(scissor, StructSize));
        scissorWriter.I32(0);
        scissorWriter.I32(0);
        scissorWriter.U32((uint)width);
        scissorWriter.U32((uint)height);
        NativeMethods.CmdSetScissor(commandBuffer, 0, 1, scissor);
    }
}
