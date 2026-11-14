// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Rendering.Vulkan.Interop;

namespace HyperPdf.Rendering.Vulkan;

/// <summary>Turns the operations of a frame into GPU work: uploads the tiles that changed, then draws everything in one render pass.</summary>
internal static unsafe class VulkanFrameRecorder
{
    /// <summary>The bytes per pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The pipeline stage and access of a finished upload, which the fragment shader waits for.</summary>
    private static readonly ImageTransition UploadDone = new(
        VkEnums.LayoutTransferDestination,
        VkEnums.LayoutShaderReadOnly,
        VkEnums.AccessTransferWrite,
        VkEnums.AccessShaderRead,
        VkEnums.StageTransfer,
        VkEnums.StageFragmentShader);

    /// <summary>The transition of an image that was never written.</summary>
    private static readonly ImageTransition BeforeFirstUpload = new(
        VkEnums.LayoutUndefined,
        VkEnums.LayoutTransferDestination,
        0,
        VkEnums.AccessTransferWrite,
        VkEnums.StageTopOfPipe,
        VkEnums.StageTransfer);

    /// <summary>The transition of an image a shader has read, before it is written again.</summary>
    private static readonly ImageTransition BeforeReupload = new(
        VkEnums.LayoutShaderReadOnly,
        VkEnums.LayoutTransferDestination,
        VkEnums.AccessShaderRead,
        VkEnums.AccessTransferWrite,
        VkEnums.StageFragmentShader,
        VkEnums.StageTransfer);

    /// <summary>Records and runs one frame.</summary>
    /// <param name="frame">The objects the frame uses.</param>
    /// <param name="ops">The operations in drawing order.</param>
    /// <param name="uploads">A list the caller keeps between frames. It is cleared here and holds the tiles uploaded this frame.</param>
    /// <param name="window">The window the finished frame is shown in, or <see langword="null"/> when the frame is only read back.</param>
    /// <exception cref="InvalidOperationException">The GPU failed.</exception>
    internal static void Render(VulkanFrameResources frame, List<VulkanDrawOp> ops, List<VulkanTileSurface> uploads, VulkanSwapchain? window)
    {
        var total = PrepareTiles(frame, ops, uploads);
        frame.EnsureStaging(total);
        var offset = 0;
        foreach (var tile in uploads)
        {
            tile.Pixels.CopyTo(frame.Staging!.Memory[offset..]);
            offset += tile.Width * tile.Height * BytesPerPixel;
        }

        frame.Commands.Begin();
        RecordUploads(frame, uploads);
        VulkanCommands.BeginFrame(frame.Commands.Buffer, frame.Pipelines.RenderPass, frame.Target.Framebuffer, frame.Target.Width, frame.Target.Height);
        RecordDraws(frame, ops);
        NativeMethods.CmdEndRenderPass(frame.Commands.Buffer);
        window?.RecordCopy(frame.Commands.Buffer, frame.Target);
        frame.Commands.SubmitAndWait();
        window?.Present();
    }

    /// <summary>Makes sure every tile in the frame has a GPU image and finds the ones whose pixels must be uploaded.</summary>
    /// <param name="frame">The objects the frame uses.</param>
    /// <param name="ops">The operations.</param>
    /// <param name="uploads">Receives the tiles to upload, each once, in the order they are staged.</param>
    /// <returns>The bytes the uploads need.</returns>
    private static int PrepareTiles(VulkanFrameResources frame, List<VulkanDrawOp> ops, List<VulkanTileSurface> uploads)
    {
        uploads.Clear();
        var total = 0;
        foreach (var op in ops)
        {
            if (op.Tile is { } tile)
            {
                total += StageIfChanged(frame, tile, uploads);
            }
        }

        return total;
    }

    /// <summary>Creates a tile's GPU image on first use and adds the tile to the uploads when its pixels are new or changed.</summary>
    /// <param name="frame">The objects the frame uses.</param>
    /// <param name="tile">The tile.</param>
    /// <param name="uploads">The tiles to upload so far.</param>
    /// <returns>The bytes the tile adds to the staging buffer, or zero when it needs no upload.</returns>
    private static int StageIfChanged(VulkanFrameResources frame, VulkanTileSurface tile, List<VulkanTileSurface> uploads)
    {
        var created = tile.Image is null;
        if (created)
        {
            CreateTileResources(frame, tile);
        }

        // Taking the dirty flag clears it, so a tile drawn twice in one frame is staged only the first time.
        var changed = tile.TakeDirty() || created;
        if (!changed)
        {
            return 0;
        }

        uploads.Add(tile);
        return tile.Width * tile.Height * BytesPerPixel;
    }

    /// <summary>Creates the GPU image of a tile and the two descriptor sets that sample it.</summary>
    /// <param name="frame">The objects the frame uses.</param>
    /// <param name="tile">The tile.</param>
    private static void CreateTileResources(VulkanFrameResources frame, VulkanTileSurface tile)
    {
        var image = new VulkanImage(frame.Context, tile.Width, tile.Height, VkEnums.ImageUsageSampled | VkEnums.ImageUsageTransferDestination);
        tile.Image = image;
        tile.NearestSet = VulkanDescriptors.Allocate(frame.Context, frame.Pipelines);
        tile.LinearSet = VulkanDescriptors.Allocate(frame.Context, frame.Pipelines);
        VulkanDescriptors.Write(frame.Context, tile.NearestSet, frame.Pipelines.NearestSampler, image.View);
        VulkanDescriptors.Write(frame.Context, tile.LinearSet, frame.Pipelines.LinearSampler, image.View);
        tile.IsFirstUpload = true;
    }

    /// <summary>Records the copy of each changed tile from the staging buffer into its image.</summary>
    /// <param name="frame">The objects the frame uses.</param>
    /// <param name="uploads">The tiles to upload.</param>
    private static void RecordUploads(VulkanFrameResources frame, List<VulkanTileSurface> uploads)
    {
        ulong offset = 0;
        foreach (var tile in uploads)
        {
            var image = tile.Image!;
            var before = tile.IsFirstUpload ? BeforeFirstUpload : BeforeReupload;
            VulkanCommands.Barrier(frame.Commands.Buffer, image.Handle, before);
            VulkanCommands.CopyBufferToImage(frame.Commands.Buffer, frame.Staging!.Handle, offset, image.Handle, tile.Width, tile.Height);
            VulkanCommands.Barrier(frame.Commands.Buffer, image.Handle, UploadDone);
            tile.IsFirstUpload = false;
            offset += (ulong)(tile.Width * tile.Height * BytesPerPixel);
        }
    }

    /// <summary>Records every operation inside the render pass.</summary>
    /// <param name="frame">The objects the frame uses.</param>
    /// <param name="ops">The operations.</param>
    private static void RecordDraws(VulkanFrameResources frame, List<VulkanDrawOp> ops)
    {
        var buffer = frame.Commands.Buffer;
        var viewportWidth = (float)frame.Target.Width;
        var viewportHeight = (float)frame.Target.Height;
        ulong bound = 0;
        foreach (var op in ops)
        {
            if (op.Kind == VulkanDrawKind.Clear)
            {
                VulkanCommands.ClearFrame(buffer, frame.Target.Width, frame.Target.Height, op.Color);
                continue;
            }

            var pipeline = op.Kind == VulkanDrawKind.Tile ? frame.Pipelines.TilePipeline : frame.Pipelines.SolidPipeline;
            if (pipeline != bound)
            {
                NativeMethods.CmdBindPipeline(buffer, VkEnums.BindPointGraphics, pipeline);
                bound = pipeline;
            }

            if (op.Tile is { } tile)
            {
                var set = op.Smooth ? tile.LinearSet : tile.NearestSet;
                NativeMethods.CmdBindDescriptorSets(buffer, VkEnums.BindPointGraphics, frame.Pipelines.PipelineLayout, 0, 1, &set, 0, null);
            }

            VulkanCommands.DrawQuad(buffer, frame.Pipelines.PipelineLayout, new(op.X, op.Y, op.Width, op.Height, viewportWidth, viewportHeight, op.Color));
        }
    }
}
