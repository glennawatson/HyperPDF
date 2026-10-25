// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Graphics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Graphics;

/// <summary>Checks standard names and the distinct legacy Compatible mode.</summary>
public sealed class PdfBlendModeNamesTests
{
    /// <summary>The standard modes are accepted and legacy or unrelated names are rejected.</summary>
    /// <param name="name">The parsed name.</param>
    /// <param name="standard">Whether the name is standard.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments((int)KnownName.Normal, true)]
    [Arguments((int)KnownName.Multiply, true)]
    [Arguments((int)KnownName.Screen, true)]
    [Arguments((int)KnownName.Overlay, true)]
    [Arguments((int)KnownName.Darken, true)]
    [Arguments((int)KnownName.Lighten, true)]
    [Arguments((int)KnownName.ColorDodge, true)]
    [Arguments((int)KnownName.ColorBurn, true)]
    [Arguments((int)KnownName.HardLight, true)]
    [Arguments((int)KnownName.SoftLight, true)]
    [Arguments((int)KnownName.Difference, true)]
    [Arguments((int)KnownName.Exclusion, true)]
    [Arguments((int)KnownName.Hue, true)]
    [Arguments((int)KnownName.Saturation, true)]
    [Arguments((int)KnownName.Color, true)]
    [Arguments((int)KnownName.Luminosity, true)]
    [Arguments((int)KnownName.Compatible, false)]
    [Arguments((int)KnownName.ColorSpace, false)]
    [Arguments(-1, false)]
    public async Task IsStandard_AllModes_PreservesClassification(int name, bool standard) =>
        await Assert.That(PdfBlendModeNames.IsStandard((KnownName)name)).IsEqualTo(standard);
}
