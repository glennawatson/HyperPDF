// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>Visits a used colour space and the spaces and profiles inside it, checking each against the PDF/A rules.</summary>
internal static class PdfAColourChecks
{
    /// <summary>The deepest colour spaces may nest before the walk stops, to end cycles.</summary>
    private const int MaxDepth = 16;

    /// <summary>The index of an ICCBased space's profile, an Indexed space's base and a Separation's colourant name.</summary>
    private const int SecondIndex = 1;

    /// <summary>The components of a CMYK ICC profile.</summary>
    private const long CmykComponents = 4;

    /// <summary>The bit in a visit variant that records overprinting.</summary>
    private const int OverprintBit = 1;

    /// <summary>The shift that places the overprint mode above the overprint bit in a visit variant.</summary>
    private const int OverprintModeShift = 1;

    /// <summary>The PDF/A part whose rules compare ICC profiles with the output intent.</summary>
    private const int Part4 = 4;

    /// <summary>Visits a colour space used outside a painting operator.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="space">The space, or null when none could be read.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Visit(PdfAColourState state, PdfAColourSpace? space) => Visit(state, space, default);

    /// <summary>Visits a used colour space.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="space">The space, or null when none could be read.</param>
    /// <param name="use">How it is used.</param>
    internal static void Visit(PdfAColourState state, PdfAColourSpace? space, in PdfAColourUse use)
    {
        if (space is not { } colourSpace || state.Depth >= MaxDepth)
        {
            return;
        }

        state.Depth++;
        try
        {
            VisitFamily(state, colourSpace, use);
        }
        finally
        {
            state.Depth--;
        }
    }

    /// <summary>Marks an indirect colour space visited; veraPDF checks each indirect colour space once.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="space">The space.</param>
    /// <returns><see langword="true"/> when it should be checked now.</returns>
    internal static bool FirstVisit(PdfAColourState state, in PdfAColourSpace space) =>
        !space.Id.IsValid || state.Visited.Add(new(space.Id, PdfAVisitKind.ColourSpace, 0));

    /// <summary>
    /// Marks a CMYK ICCBased space that has no object of its own visited, with no overprint. veraPDF keys every such
    /// space alike, so this also stops later direct CMYK spaces with the same settings from being checked.
    /// </summary>
    /// <param name="state">The walk.</param>
    /// <returns><see langword="true"/> when it should be checked now.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool FirstDirectCmykVisit(PdfAColourState state) => state.Visited.Add(new(default, PdfAVisitKind.CmykColourSpace, 0));

    /// <summary>
    /// Checks the PDF/A-4 rule that a CMYK ICCBased space does not use the output intent's or the blending space's
    /// profile, comparing objects and then contents.
    /// </summary>
    /// <param name="state">The walk.</param>
    /// <param name="identity">The profile's identity, or null when it cannot be read.</param>
    /// <param name="profileId">The profile's object, when indirect.</param>
    internal static void CheckProfileReuse(PdfAColourState state, byte[]? identity, PdfObjectId profileId)
    {
        var blending = BlendingProfile(state, out var blendingId);
        var reused = (profileId.IsValid && (profileId == state.OutputProfileId || profileId == blendingId))
            || (identity is not null && (SameIdentity(state, identity, state.OutputProfile) || SameIdentity(state, identity, blending)));
        state.Context.Require(!reused, PdfARule.IccCmykOverprintProfile, null);
    }

    /// <summary>Checks a space by its family.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="space">The space.</param>
    /// <param name="use">How it is used.</param>
    private static void VisitFamily(PdfAColourState state, in PdfAColourSpace space, in PdfAColourUse use)
    {
        var family = PdfAColourSpaceParsing.Family(space);
        switch (family)
        {
            case KnownName.DeviceGray or KnownName.DeviceRGB or KnownName.DeviceCMYK:
            {
                PdfADeviceChecks.Check(state, family);
                break;
            }

            case KnownName.ICCBased:
            {
                VisitIccBased(state, space, use);
                break;
            }

            case KnownName.Separation:
            {
                PdfASeparationChecks.VisitSeparation(state, space);
                break;
            }

            case KnownName.DeviceN:
            {
                PdfASeparationChecks.VisitDeviceN(state, space);
                break;
            }

            case KnownName.Indexed:
            {
                VisitIndexed(state, space);
                break;
            }

            case KnownName.Pattern:
            {
                VisitPattern(state, space, use);
                break;
            }

            default:
            {
                break;
            }
        }
    }

    /// <summary>Visits an Indexed space and its base.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="space">The space.</param>
    private static void VisitIndexed(PdfAColourState state, in PdfAColourSpace space)
    {
        if (!FirstVisit(state, space))
        {
            return;
        }

        var array = space.Value.AsArray()!;
        Visit(state, PdfAColourSpaceParsing.Parse(state, array.GetRaw(SecondIndex), space.Resources, false));
    }

    /// <summary>Visits a pattern painted with: a tiling pattern's content or a shading pattern's shading.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="space">The pattern.</param>
    /// <param name="use">How it is used.</param>
    private static void VisitPattern(PdfAColourState state, in PdfAColourSpace space, in PdfAColourUse use)
    {
        // A /Pattern name or [/Pattern base] array is not a pattern object, and veraPDF checks nothing for it.
        if (space.Value.AsDictionary() is not null && use.Frame is { } frame)
        {
            PdfAPatternWalk.Visit(state, space, frame);
        }
    }

    /// <summary>Visits an ICCBased space and its profile.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="space">The space.</param>
    /// <param name="use">How it is used.</param>
    private static void VisitIccBased(PdfAColourState state, in PdfAColourSpace space, in PdfAColourUse use)
    {
        var profile = StoreReading.Resolve(state.Store, space.Value.AsArray()!.GetRaw(SecondIndex)).AsStream();
        var components = profile?.Dictionary.Get(KnownName.N) ?? default;
        var isCmyk = components.IsNumber && components.AsInteger() == CmykComponents;
        if (isCmyk ? !FirstCmykVisit(state, space, use) : !FirstVisit(state, space))
        {
            return;
        }

        if (isCmyk)
        {
            CheckCmykUse(state, profile!, space, use);
        }

        if (profile is not null)
        {
            PdfAIccChecks.CheckInputProfile(state, profile);
        }
    }

    /// <summary>
    /// Marks a CMYK ICCBased space visited with its overprint settings. veraPDF keys these visits by object and settings,
    /// and every direct CMYK space shares one key, so only the first direct one with given settings is checked.
    /// </summary>
    /// <param name="state">The walk.</param>
    /// <param name="space">The space.</param>
    /// <param name="use">How it is used.</param>
    /// <returns><see langword="true"/> when it should be checked now.</returns>
    private static bool FirstCmykVisit(PdfAColourState state, in PdfAColourSpace space, in PdfAColourUse use)
    {
        var variant = (use.OverprintMode << OverprintModeShift) | (use.Overprint ? OverprintBit : 0);
        return state.Visited.Add(new(space.Id, PdfAVisitKind.CmykColourSpace, variant));
    }

    /// <summary>Checks the overprint rules for a CMYK ICCBased space.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="profile">The profile.</param>
    /// <param name="space">The space.</param>
    /// <param name="use">How it is used.</param>
    private static void CheckCmykUse(PdfAColourState state, PdfStream profile, in PdfAColourSpace space, in PdfAColourUse use)
    {
        state.Context.Require(!use.Overprint || use.OverprintMode == 0, PdfARule.IccCmykOverprintMode, null);
        if (state.Context.Part != Part4)
        {
            return;
        }

        var raw = space.Value.AsArray()!.GetRaw(SecondIndex);
        CheckProfileReuse(state, PdfAIccProfiles.Identity(state, profile), raw.IsReference ? raw.AsReference() : default);
    }

    /// <summary>Gets the profile of the current blending space when it is ICCBased.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="id">The profile's object when indirect.</param>
    /// <returns>The profile, or null.</returns>
    private static PdfStream? BlendingProfile(PdfAColourState state, out PdfObjectId id)
    {
        id = default;
        if (state.CurrentBlendingSpace is not { } blending || PdfAColourSpaceParsing.Family(blending) != KnownName.ICCBased)
        {
            return null;
        }

        var raw = blending.Value.AsArray()!.GetRaw(SecondIndex);
        id = raw.IsReference ? raw.AsReference() : default;
        return StoreReading.Resolve(state.Store, raw).AsStream();
    }

    /// <summary>Compares a profile identity with another profile's.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="identity">The identity.</param>
    /// <param name="other">The other profile, or null.</param>
    /// <returns><see langword="true"/> when they match.</returns>
    private static bool SameIdentity(PdfAColourState state, byte[] identity, PdfStream? other) =>
        other is not null && PdfAIccProfiles.Identity(state, other) is { } otherIdentity && identity.AsSpan().SequenceEqual(otherIdentity);
}
