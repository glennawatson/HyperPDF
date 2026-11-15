// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Settings;

namespace HyperPdf.Windowing.Rendering;

/// <summary>Turns what the user chose, in the settings or in an environment variable, into the preference a window opens with.</summary>
public static class RenderBackendChoices
{
    /// <summary>The environment variable that picks the render backend. It wins over the settings.</summary>
    private const string EnvironmentVariable = "HYPERPDF_RENDER_BACKEND";

    /// <summary>The names a choice goes by, in capitals.</summary>
    private static readonly Dictionary<string, RenderBackendPreference> Names = new(StringComparer.Ordinal)
    {
        ["CPU"] = RenderBackendPreference.Cpu,
        ["PROCESSOR"] = RenderBackendPreference.Cpu,
        ["SOFTWARE"] = RenderBackendPreference.Cpu,
        ["GPU"] = RenderBackendPreference.Gpu,
        ["AUTO"] = RenderBackendPreference.Gpu,
        ["AUTOMATIC"] = RenderBackendPreference.Gpu,
        ["D3D11"] = RenderBackendPreference.Direct3D11,
        ["DIRECT3D11"] = RenderBackendPreference.Direct3D11,
        ["DX11"] = RenderBackendPreference.Direct3D11,
        ["D3D12"] = RenderBackendPreference.Direct3D12,
        ["DIRECT3D12"] = RenderBackendPreference.Direct3D12,
        ["DX12"] = RenderBackendPreference.Direct3D12,
        ["VULKAN"] = RenderBackendPreference.Vulkan,
        ["VK"] = RenderBackendPreference.Vulkan,
    };

    /// <summary>Reads the choice from the environment variable first, and from the settings when the variable is not set.</summary>
    /// <param name="setting">The choice in the settings.</param>
    /// <returns>The preference.</returns>
    public static RenderBackendPreference Choose(RenderBackendSetting setting) => Parse(Environment.GetEnvironmentVariable(EnvironmentVariable)) ?? From(setting);

    /// <summary>Turns a choice of the settings into a preference.</summary>
    /// <param name="setting">The choice.</param>
    /// <returns>The preference.</returns>
    public static RenderBackendPreference From(RenderBackendSetting setting) => setting switch
    {
        RenderBackendSetting.Direct3D11 => RenderBackendPreference.Direct3D11,
        RenderBackendSetting.Direct3D12 => RenderBackendPreference.Direct3D12,
        RenderBackendSetting.Vulkan => RenderBackendPreference.Vulkan,
        RenderBackendSetting.Processor => RenderBackendPreference.Cpu,
        _ => RenderBackendPreference.Gpu,
    };

    /// <summary>Reads a choice written as text.</summary>
    /// <param name="text">The text: <c>cpu</c>, <c>gpu</c>, <c>auto</c>, <c>d3d11</c>, <c>d3d12</c> or <c>vulkan</c>, in any case.</param>
    /// <returns>The preference, or <see langword="null"/> when the text is empty or names none.</returns>
    public static RenderBackendPreference? Parse(string? text) =>
        text is not null && Names.TryGetValue(text.Trim().ToUpperInvariant(), out var preference) ? preference : null;
}
