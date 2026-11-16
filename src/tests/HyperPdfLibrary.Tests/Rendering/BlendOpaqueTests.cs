// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Graphics;
using HyperPdfLibrary.Render.Managed.Raster;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>Checks that blending two opaque pixels the short way gives the bytes the general blend gives.</summary>
public sealed class BlendOpaqueTests
{
    /// <summary>The pixels tried for each mode.</summary>
    private const int Pixels = 4096;

    /// <summary>The factor that spreads the test pixels over every colour byte.</summary>
    private const uint Spread = 2_654_435_761U;

    /// <summary>The bits of an opaque pixel's alpha byte.</summary>
    private const uint Opaque = 0xFF000000U;

    /// <summary>The first and last blend modes, so every mode is tried.</summary>
    private const int LastMode = (int)PdfBlendMode.Luminosity;

    /// <summary>Every mode gives the same bytes for opaque pixels by the short way as by the general way.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ShortWayMatchesTheGeneralWay()
    {
        var differences = 0;
        for (var mode = 0; mode <= LastMode; mode++)
        {
            for (var i = 1; i <= Pixels; i++)
            {
                var source = ((uint)i * Spread) | Opaque;
                var backdrop = ((uint)(i + Pixels) * Spread) | Opaque;
                if (BlendModes.BlendOpaque((PdfBlendMode)mode, source, backdrop) != BlendModes.Blend((PdfBlendMode)mode, source, backdrop))
                {
                    differences++;
                }
            }
        }

        await Assert.That(differences).IsEqualTo(0);
    }
}
