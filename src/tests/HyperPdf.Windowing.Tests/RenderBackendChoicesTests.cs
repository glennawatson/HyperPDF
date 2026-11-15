// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Settings;
using HyperPdf.Windowing.Rendering;

namespace HyperPdf.Windowing.Tests;

/// <summary>Checks how a choice in the settings or an environment variable becomes the preference a window opens with.</summary>
public sealed class RenderBackendChoicesTests
{
    /// <summary>Every setting maps to its own preference.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EverySettingMapsToItsPreference()
    {
        await Assert.That(RenderBackendChoices.From(RenderBackendSetting.Automatic)).IsEqualTo(RenderBackendPreference.Gpu);
        await Assert.That(RenderBackendChoices.From(RenderBackendSetting.Direct3D11)).IsEqualTo(RenderBackendPreference.Direct3D11);
        await Assert.That(RenderBackendChoices.From(RenderBackendSetting.Direct3D12)).IsEqualTo(RenderBackendPreference.Direct3D12);
        await Assert.That(RenderBackendChoices.From(RenderBackendSetting.Vulkan)).IsEqualTo(RenderBackendPreference.Vulkan);
        await Assert.That(RenderBackendChoices.From(RenderBackendSetting.Processor)).IsEqualTo(RenderBackendPreference.Cpu);
    }

    /// <summary>Text names are read in any case, and the short names work too.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TextNamesAreReadInAnyCase()
    {
        await Assert.That(RenderBackendChoices.Parse("d3d12")).IsEqualTo(RenderBackendPreference.Direct3D12);
        await Assert.That(RenderBackendChoices.Parse(" DX11 ")).IsEqualTo(RenderBackendPreference.Direct3D11);
        await Assert.That(RenderBackendChoices.Parse("Vulkan")).IsEqualTo(RenderBackendPreference.Vulkan);
        await Assert.That(RenderBackendChoices.Parse("cpu")).IsEqualTo(RenderBackendPreference.Cpu);
        await Assert.That(RenderBackendChoices.Parse("auto")).IsEqualTo(RenderBackendPreference.Gpu);
    }

    /// <summary>Empty or unknown text names no choice, so the settings decide.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnknownTextNamesNoChoice()
    {
        await Assert.That(RenderBackendChoices.Parse(null)).IsNull();
        await Assert.That(RenderBackendChoices.Parse(string.Empty)).IsNull();
        await Assert.That(RenderBackendChoices.Parse("metal3000")).IsNull();
    }
}
