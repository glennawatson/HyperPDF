// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Rendering.Direct3D.Interop;

/// <summary>D3D11_SAMPLER_DESC, in the field order and layout of the C structure.</summary>
/// <param name="Filter">The filter.</param>
/// <param name="AddressU">The address mode across.</param>
/// <param name="AddressV">The address mode down.</param>
/// <param name="AddressW">The address mode in depth.</param>
/// <param name="MipLodBias">The mip level bias.</param>
/// <param name="MaxAnisotropy">The anisotropy limit.</param>
/// <param name="ComparisonFunction">The comparison function.</param>
/// <param name="BorderRed">The border colour red.</param>
/// <param name="BorderGreen">The border colour green.</param>
/// <param name="BorderBlue">The border colour blue.</param>
/// <param name="BorderAlpha">The border colour alpha.</param>
/// <param name="MinLod">The smallest mip level.</param>
/// <param name="MaxLod">The largest mip level.</param>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct SamplerDescription(
    uint Filter,
    uint AddressU,
    uint AddressV,
    uint AddressW,
    float MipLodBias,
    uint MaxAnisotropy,
    uint ComparisonFunction,
    float BorderRed,
    float BorderGreen,
    float BorderBlue,
    float BorderAlpha,
    float MinLod,
    float MaxLod);
