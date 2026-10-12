// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Tracks transparency blending spaces, which PDF/A-4 accepts in place of an output intent for device colour, and visits
/// the colour spaces of transparency groups.
/// </summary>
internal static class PdfATransparency
{
    /// <summary>The components of a grey space.</summary>
    private const int GrayComponents = 1;

    /// <summary>The components of an RGB space.</summary>
    private const int RgbComponents = 3;

    /// <summary>The components of a CMYK space.</summary>
    private const int CmykComponents = 4;

    /// <summary>The index of an ICCBased space's profile.</summary>
    private const int ProfileIndex = 1;

    /// <summary>Gets the blending space of a page: the colour space of its transparency group.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="group">The page's group dictionary, or null.</param>
    /// <returns>The space, or null.</returns>
    internal static PdfAColourSpace? PageBlendingSpace(PdfAColourState state, PdfDictionary? group) =>
        group is not null && group.IsName(KnownName.S, KnownName.Transparency)
            ? PdfAColourSpaceParsing.Parse(state, group.GetRaw(KnownName.CS), null, false)
            : null;

    /// <summary>
    /// Gets the blending space a form sets: the colour space of an isolated transparency group, unless it is a device
    /// space that the current blending space already gives a device-independent form of.
    /// </summary>
    /// <param name="state">The walk.</param>
    /// <param name="group">The form's group dictionary, or null.</param>
    /// <returns>The space, or null when the form sets none.</returns>
    internal static PdfAColourSpace? FormBlendingSpace(PdfAColourState state, PdfDictionary? group)
    {
        if (group is null || !group.IsName(KnownName.S, KnownName.Transparency) || !group.Get(KnownName.I).AsBoolean(false))
        {
            return null;
        }

        var space = PdfAColourSpaceParsing.Parse(state, group.GetRaw(KnownName.CS), null, false);
        if (state.CurrentBlendingSpace is not { } current || space is not { } own)
        {
            return space;
        }

        return IsCoveredBy(state, PdfAColourSpaceParsing.Family(own), current) ? null : own;
    }

    /// <summary>Makes a space the current blending space and records its type.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="space">The space, or null to clear it.</param>
    internal static void Enter(PdfAColourState state, PdfAColourSpace? space)
    {
        state.CurrentBlendingSpace = space;
        state.TransparencySpace = space is { } known ? TypeText(state, known) : null;
    }

    /// <summary>Visits the colour space of a group, read with the resources given.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="rawGroup">The group, possibly a reference.</param>
    /// <param name="resources">The resources names in it are read from, or null.</param>
    internal static void VisitGroupSpace(PdfAColourState state, PdfValue rawGroup, PdfDictionary? resources)
    {
        if (StoreReading.Resolve(state.Store, rawGroup).AsDictionary() is not { } group)
        {
            return;
        }

        PdfAColourChecks.Visit(state, PdfAColourSpaceParsing.Parse(state, group.GetRaw(KnownName.CS), resources, false));
    }

    /// <summary>Gets the type veraPDF records for a blending space: the ICC data colour space, or the family name.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="space">The space.</param>
    /// <returns>The type, or null for an ICCBased space without a readable profile.</returns>
    private static string? TypeText(PdfAColourState state, in PdfAColourSpace space)
    {
        var family = PdfAColourSpaceParsing.Family(space);
        if (family != KnownName.ICCBased)
        {
            return state.Store.Names.GetString(family);
        }

        var profile = StoreReading.Resolve(state.Store, space.Value.AsArray()!.GetRaw(ProfileIndex)).AsStream();
        return profile is null ? null : PdfAIccProfiles.SpaceText(PdfAIccProfiles.ReadHeader(state, profile).ColourSpace);
    }

    /// <summary>Determines whether the current blending space already gives a device-independent form of a device family.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="family">The form group's family.</param>
    /// <param name="current">The current blending space.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool IsCoveredBy(PdfAColourState state, KnownName family, in PdfAColourSpace current)
    {
        var currentFamily = PdfAColourSpaceParsing.Family(current);
        var components = currentFamily == KnownName.ICCBased ? Components(state, current) : 0;
        return family switch
        {
            KnownName.DeviceRGB => currentFamily is KnownName.CalRGB or KnownName.Lab || components == RgbComponents,
            KnownName.DeviceCMYK => components == CmykComponents,
            KnownName.DeviceGray => currentFamily == KnownName.CalGray || components == GrayComponents,
            _ => false,
        };
    }

    /// <summary>Gets the <c>/N</c> of an ICCBased space's profile.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="space">The space.</param>
    /// <returns>The component count, or -1.</returns>
    private static long Components(PdfAColourState state, in PdfAColourSpace space) =>
        StoreReading.Resolve(state.Store, space.Value.AsArray()!.GetRaw(ProfileIndex)).AsStream()?.Dictionary.Get(KnownName.N).AsInteger(-1) ?? -1;
}
