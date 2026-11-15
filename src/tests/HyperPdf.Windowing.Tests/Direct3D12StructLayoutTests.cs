// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using HyperPdf.Rendering.Direct3D12.Interop;

namespace HyperPdf.Windowing.Tests;

/// <summary>Checks that the structures handed to Direct3D 12 have the sizes of the C structures in d3d12.h and dxgi1_2.h, on every platform.</summary>
[SupportedOSPlatform("windows")]
public sealed class Direct3D12StructLayoutTests
{
    /// <summary>The size of D3D12_GRAPHICS_PIPELINE_STATE_DESC on 64 bit Windows.</summary>
    private const int PipelineDescriptionSize = 656;

    /// <summary>The size of D3D12_BLEND_DESC.</summary>
    private const int BlendDescriptionSize = 328;

    /// <summary>The size of D3D12_RESOURCE_DESC.</summary>
    private const int ResourceDescriptionSize = 56;

    /// <summary>The size of D3D12_RESOURCE_BARRIER and D3D12_ROOT_PARAMETER.</summary>
    private const int ThirtyTwoByteSize = 32;

    /// <summary>The size of D3D12_TEXTURE_COPY_LOCATION and DXGI_SWAP_CHAIN_DESC1.</summary>
    private const int FortyEightByteSize = 48;

    /// <summary>The size of D3D12_DEPTH_STENCIL_DESC.</summary>
    private const int DepthStencilDescriptionSize = 52;

    /// <summary>The size of D3D12_RASTERIZER_DESC.</summary>
    private const int RasterizerDescriptionSize = 44;

    /// <summary>The size of D3D12_SAMPLER_DESC.</summary>
    private const int SamplerDescriptionSize = 52;

    /// <summary>The size of D3D12_ROOT_SIGNATURE_DESC.</summary>
    private const int RootSignatureDescriptionSize = 40;

    /// <summary>The size of D3D12_HEAP_PROPERTIES and D3D12_DESCRIPTOR_RANGE.</summary>
    private const int TwentyByteSize = 20;

    /// <summary>The size of D3D12_INPUT_ELEMENT_DESC, D3D12_STREAM_OUTPUT_DESC and D3D12_RENDER_TARGET_BLEND_DESC less one field.</summary>
    private const int InputElementSize = 32;

    /// <summary>The size of D3D12_RENDER_TARGET_BLEND_DESC.</summary>
    private const int TargetBlendSize = 40;

    /// <summary>The size of D3D12_VERTEX_BUFFER_VIEW and D3D12_COMMAND_QUEUE_DESC.</summary>
    private const int SixteenByteSize = 16;

    /// <summary>The big structures match their C layout, including the padding the 64 bit pointers cause.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LargeStructuresMatchTheirCLayout()
    {
        await Assert.That(Unsafe.SizeOf<GraphicsPipelineDescription>()).IsEqualTo(PipelineDescriptionSize);
        await Assert.That(Unsafe.SizeOf<BlendDescription>()).IsEqualTo(BlendDescriptionSize);
        await Assert.That(Unsafe.SizeOf<ResourceDescription>()).IsEqualTo(ResourceDescriptionSize);
        await Assert.That(Unsafe.SizeOf<ResourceBarrier>()).IsEqualTo(ThirtyTwoByteSize);
        await Assert.That(Unsafe.SizeOf<RootParameter>()).IsEqualTo(ThirtyTwoByteSize);
        await Assert.That(Unsafe.SizeOf<TextureCopyLocation>()).IsEqualTo(FortyEightByteSize);
        await Assert.That(Unsafe.SizeOf<SwapChainDescription1>()).IsEqualTo(FortyEightByteSize);
    }

    /// <summary>The small structures match their C layout.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SmallStructuresMatchTheirCLayout()
    {
        await Assert.That(Unsafe.SizeOf<DepthStencilDescription>()).IsEqualTo(DepthStencilDescriptionSize);
        await Assert.That(Unsafe.SizeOf<RasterizerDescription>()).IsEqualTo(RasterizerDescriptionSize);
        await Assert.That(Unsafe.SizeOf<SamplerDescription>()).IsEqualTo(SamplerDescriptionSize);
        await Assert.That(Unsafe.SizeOf<RootSignatureDescription>()).IsEqualTo(RootSignatureDescriptionSize);
        await Assert.That(Unsafe.SizeOf<HeapProperties>()).IsEqualTo(TwentyByteSize);
        await Assert.That(Unsafe.SizeOf<DescriptorRange>()).IsEqualTo(TwentyByteSize);
        await Assert.That(Unsafe.SizeOf<InputElementDescription>()).IsEqualTo(InputElementSize);
        await Assert.That(Unsafe.SizeOf<StreamOutputDescription>()).IsEqualTo(InputElementSize);
        await Assert.That(Unsafe.SizeOf<RenderTargetBlend>()).IsEqualTo(TargetBlendSize);
        await Assert.That(Unsafe.SizeOf<VertexBufferView>()).IsEqualTo(SixteenByteSize);
        await Assert.That(Unsafe.SizeOf<CommandQueueDescription>()).IsEqualTo(SixteenByteSize);
    }
}
