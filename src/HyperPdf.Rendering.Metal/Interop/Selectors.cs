// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Rendering.Metal.Interop;

/// <summary>The Objective-C selectors the backend sends, looked up once. A selector never changes for the life of the process.</summary>
internal static class Selectors
{
    /// <summary>Gets <c>alloc</c>.</summary>
    internal static nint Alloc { get; } = NativeMethods.Selector("alloc");

    /// <summary>Gets <c>init</c>.</summary>
    internal static nint Init { get; } = NativeMethods.Selector("init");

    /// <summary>Gets <c>release</c>.</summary>
    internal static nint Release { get; } = NativeMethods.Selector("release");

    /// <summary>Gets <c>stringWithUTF8String:</c>.</summary>
    internal static nint StringWithUtf8 { get; } = NativeMethods.Selector("stringWithUTF8String:");

    /// <summary>Gets <c>newCommandQueue</c>.</summary>
    internal static nint NewCommandQueue { get; } = NativeMethods.Selector("newCommandQueue");

    /// <summary>Gets <c>newLibraryWithSource:options:error:</c>.</summary>
    internal static nint NewLibraryWithSource { get; } = NativeMethods.Selector("newLibraryWithSource:options:error:");

    /// <summary>Gets <c>newFunctionWithName:</c>.</summary>
    internal static nint NewFunctionWithName { get; } = NativeMethods.Selector("newFunctionWithName:");

    /// <summary>Gets <c>setVertexFunction:</c>.</summary>
    internal static nint SetVertexFunction { get; } = NativeMethods.Selector("setVertexFunction:");

    /// <summary>Gets <c>setFragmentFunction:</c>.</summary>
    internal static nint SetFragmentFunction { get; } = NativeMethods.Selector("setFragmentFunction:");

    /// <summary>Gets <c>colorAttachments</c>.</summary>
    internal static nint ColorAttachments { get; } = NativeMethods.Selector("colorAttachments");

    /// <summary>Gets <c>objectAtIndexedSubscript:</c>.</summary>
    internal static nint ObjectAtIndexedSubscript { get; } = NativeMethods.Selector("objectAtIndexedSubscript:");

    /// <summary>Gets <c>setPixelFormat:</c>.</summary>
    internal static nint SetPixelFormat { get; } = NativeMethods.Selector("setPixelFormat:");

    /// <summary>Gets <c>setBlendingEnabled:</c>.</summary>
    internal static nint SetBlendingEnabled { get; } = NativeMethods.Selector("setBlendingEnabled:");

    /// <summary>Gets <c>setSourceRGBBlendFactor:</c>.</summary>
    internal static nint SetSourceRgbBlendFactor { get; } = NativeMethods.Selector("setSourceRGBBlendFactor:");

    /// <summary>Gets <c>setSourceAlphaBlendFactor:</c>.</summary>
    internal static nint SetSourceAlphaBlendFactor { get; } = NativeMethods.Selector("setSourceAlphaBlendFactor:");

    /// <summary>Gets <c>setDestinationRGBBlendFactor:</c>.</summary>
    internal static nint SetDestinationRgbBlendFactor { get; } = NativeMethods.Selector("setDestinationRGBBlendFactor:");

    /// <summary>Gets <c>setDestinationAlphaBlendFactor:</c>.</summary>
    internal static nint SetDestinationAlphaBlendFactor { get; } = NativeMethods.Selector("setDestinationAlphaBlendFactor:");

    /// <summary>Gets <c>newRenderPipelineStateWithDescriptor:error:</c>.</summary>
    internal static nint NewRenderPipelineState { get; } = NativeMethods.Selector("newRenderPipelineStateWithDescriptor:error:");

    /// <summary>Gets <c>setMinFilter:</c>.</summary>
    internal static nint SetMinFilter { get; } = NativeMethods.Selector("setMinFilter:");

    /// <summary>Gets <c>setMagFilter:</c>.</summary>
    internal static nint SetMagFilter { get; } = NativeMethods.Selector("setMagFilter:");

    /// <summary>Gets <c>newSamplerStateWithDescriptor:</c>.</summary>
    internal static nint NewSamplerState { get; } = NativeMethods.Selector("newSamplerStateWithDescriptor:");

    /// <summary>Gets <c>texture2DDescriptorWithPixelFormat:width:height:mipmapped:</c>.</summary>
    internal static nint Texture2DDescriptor { get; } = NativeMethods.Selector("texture2DDescriptorWithPixelFormat:width:height:mipmapped:");

    /// <summary>Gets <c>setUsage:</c>.</summary>
    internal static nint SetUsage { get; } = NativeMethods.Selector("setUsage:");

    /// <summary>Gets <c>newTextureWithDescriptor:</c>.</summary>
    internal static nint NewTexture { get; } = NativeMethods.Selector("newTextureWithDescriptor:");

    /// <summary>Gets <c>replaceRegion:mipmapLevel:withBytes:bytesPerRow:</c>.</summary>
    internal static nint ReplaceRegion { get; } = NativeMethods.Selector("replaceRegion:mipmapLevel:withBytes:bytesPerRow:");

    /// <summary>Gets <c>getBytes:bytesPerRow:fromRegion:mipmapLevel:</c>.</summary>
    internal static nint GetBytes { get; } = NativeMethods.Selector("getBytes:bytesPerRow:fromRegion:mipmapLevel:");

    /// <summary>Gets <c>newBufferWithLength:options:</c>.</summary>
    internal static nint NewBuffer { get; } = NativeMethods.Selector("newBufferWithLength:options:");

    /// <summary>Gets <c>contents</c>.</summary>
    internal static nint Contents { get; } = NativeMethods.Selector("contents");

    /// <summary>Gets <c>setDevice:</c>.</summary>
    internal static nint SetDevice { get; } = NativeMethods.Selector("setDevice:");

    /// <summary>Gets <c>setFramebufferOnly:</c>.</summary>
    internal static nint SetFramebufferOnly { get; } = NativeMethods.Selector("setFramebufferOnly:");

    /// <summary>Gets <c>setDrawableSize:</c>.</summary>
    internal static nint SetDrawableSize { get; } = NativeMethods.Selector("setDrawableSize:");

    /// <summary>Gets <c>nextDrawable</c>.</summary>
    internal static nint NextDrawable { get; } = NativeMethods.Selector("nextDrawable");

    /// <summary>Gets <c>texture</c>.</summary>
    internal static nint Texture { get; } = NativeMethods.Selector("texture");

    /// <summary>Gets <c>renderPassDescriptor</c>.</summary>
    internal static nint RenderPassDescriptor { get; } = NativeMethods.Selector("renderPassDescriptor");

    /// <summary>Gets <c>setTexture:</c>.</summary>
    internal static nint SetTexture { get; } = NativeMethods.Selector("setTexture:");

    /// <summary>Gets <c>setLoadAction:</c>.</summary>
    internal static nint SetLoadAction { get; } = NativeMethods.Selector("setLoadAction:");

    /// <summary>Gets <c>setStoreAction:</c>.</summary>
    internal static nint SetStoreAction { get; } = NativeMethods.Selector("setStoreAction:");

    /// <summary>Gets <c>setClearColor:</c>.</summary>
    internal static nint SetClearColor { get; } = NativeMethods.Selector("setClearColor:");

    /// <summary>Gets <c>commandBuffer</c>.</summary>
    internal static nint CommandBuffer { get; } = NativeMethods.Selector("commandBuffer");

    /// <summary>Gets <c>renderCommandEncoderWithDescriptor:</c>.</summary>
    internal static nint RenderCommandEncoder { get; } = NativeMethods.Selector("renderCommandEncoderWithDescriptor:");

    /// <summary>Gets <c>setRenderPipelineState:</c>.</summary>
    internal static nint SetRenderPipelineState { get; } = NativeMethods.Selector("setRenderPipelineState:");

    /// <summary>Gets <c>setVertexBuffer:offset:atIndex:</c>.</summary>
    internal static nint SetVertexBuffer { get; } = NativeMethods.Selector("setVertexBuffer:offset:atIndex:");

    /// <summary>Gets <c>setVertexBytes:length:atIndex:</c>.</summary>
    internal static nint SetVertexBytes { get; } = NativeMethods.Selector("setVertexBytes:length:atIndex:");

    /// <summary>Gets <c>setFragmentTexture:atIndex:</c>.</summary>
    internal static nint SetFragmentTexture { get; } = NativeMethods.Selector("setFragmentTexture:atIndex:");

    /// <summary>Gets <c>setFragmentSamplerState:atIndex:</c>.</summary>
    internal static nint SetFragmentSamplerState { get; } = NativeMethods.Selector("setFragmentSamplerState:atIndex:");

    /// <summary>Gets <c>drawPrimitives:vertexStart:vertexCount:</c>.</summary>
    internal static nint DrawPrimitives { get; } = NativeMethods.Selector("drawPrimitives:vertexStart:vertexCount:");

    /// <summary>Gets <c>endEncoding</c>.</summary>
    internal static nint EndEncoding { get; } = NativeMethods.Selector("endEncoding");

    /// <summary>Gets <c>presentDrawable:</c>.</summary>
    internal static nint PresentDrawable { get; } = NativeMethods.Selector("presentDrawable:");

    /// <summary>Gets <c>commit</c>.</summary>
    internal static nint Commit { get; } = NativeMethods.Selector("commit");

    /// <summary>Gets <c>waitUntilCompleted</c>.</summary>
    internal static nint WaitUntilCompleted { get; } = NativeMethods.Selector("waitUntilCompleted");

    /// <summary>Gets <c>storageMode</c>.</summary>
    internal static nint StorageMode { get; } = NativeMethods.Selector("storageMode");

    /// <summary>Gets <c>blitCommandEncoder</c>.</summary>
    internal static nint BlitCommandEncoder { get; } = NativeMethods.Selector("blitCommandEncoder");

    /// <summary>Gets <c>synchronizeResource:</c>.</summary>
    internal static nint SynchronizeResource { get; } = NativeMethods.Selector("synchronizeResource:");
}
