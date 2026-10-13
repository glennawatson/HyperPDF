// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Graphics.Colors;

/// <summary>
/// A look-up table ICC profile: its AToB tags and the transforms made from them, one per intent table and black point
/// choice. Transforms are created on request and shared; the profile bytes stay alive for that.
/// </summary>
[DebuggerDisplay("IccLutProfile: {_components} components, version {_version}")]
internal sealed class IccLutProfile
{
    /// <summary>The 'A2B0' tag, the perceptual table.</summary>
    private const uint PerceptualTag = 0x41324230;

    /// <summary>The 'A2B1' tag, the relative colorimetric table.</summary>
    private const uint RelativeTag = 0x41324231;

    /// <summary>The 'A2B2' tag, the saturation table.</summary>
    private const uint SaturationTag = 0x41324232;

    /// <summary>The first profile version that uses the v4 black point rules.</summary>
    private const int FirstVersion4 = 4;

    /// <summary>The highest L* the darkest colorant may have for black point detection.</summary>
    private const float MaxBlackLightness = 50F;

    /// <summary>The number of intents with a table of their own: perceptual, relative colorimetric and saturation.</summary>
    private const int IntentSlots = 3;

    /// <summary>The black point choices of each table: without and with compensation.</summary>
    private const int CompensationChoices = 2;

    /// <summary>The number of transforms: each intent table with and without black point compensation.</summary>
    private const int TransformSlots = IntentSlots * CompensationChoices;

    /// <summary>The slot of the relative colorimetric transform.</summary>
    private const int RelativeSlot = 1;

    /// <summary>The slot of the saturation transform.</summary>
    private const int SaturationSlot = 2;

    /// <summary>The perceptual black point of v4 profiles in D50 XYZ.</summary>
    private static readonly Float3 PerceptualBlack = new(0.00336F, 0.0034731F, 0.00287F);

    /// <summary>The profile bytes.</summary>
    private readonly byte[] _profile;

    /// <summary>The number of device channels.</summary>
    private readonly int _components;

    /// <summary>Whether the profile connection space is Lab.</summary>
    private readonly bool _labPcs;

    /// <summary>The major version of the profile.</summary>
    private readonly int _version;

    /// <summary>The device values of the darkest colour, or <see langword="null"/> for a space with no known darkest colour.</summary>
    private readonly float[]? _darkest;

    /// <summary>The transform of each intent table and compensation choice; a slot is filled on first request.</summary>
    private readonly IccLutTransform?[] _transforms = new IccLutTransform?[TransformSlots];

    /// <summary>Guards filling the transform slots.</summary>
    private readonly Lock _gate = new();

    /// <summary>Initializes a new instance of the <see cref="IccLutProfile"/> class.</summary>
    /// <param name="profile">The profile bytes.</param>
    /// <param name="shape">The profile's colour spaces and version.</param>
    private IccLutProfile(byte[] profile, IccLutShape shape)
    {
        _profile = profile;
        _components = shape.Components;
        _labPcs = shape.LabPcs;
        _version = shape.Version;
        _darkest = shape.Darkest;
        HasLabInput = shape.LabInput;
    }

    /// <summary>Gets a value indicating whether the device colour space is Lab, so components arrive as L*, a* and b*.</summary>
    internal bool HasLabInput { get; }

    /// <summary>Reads a profile's perceptual table.</summary>
    /// <param name="profile">The profile bytes; the profile keeps the array.</param>
    /// <param name="shape">The profile's colour spaces and version.</param>
    /// <returns>The profile, or <see langword="null"/> when it has no readable AToB0 table.</returns>
    internal static IccLutProfile? Create(byte[] profile, IccLutShape shape)
    {
        var created = new IccLutProfile(profile, shape);
        return created.Parse(PerceptualTag) is null ? null : created;
    }

    /// <summary>Gets the transform for a rendering intent and black point choice.</summary>
    /// <param name="rendering">The rendering; absolute colorimetric never compensates (ISO 32000-2 8.6.5.9).</param>
    /// <returns>The transform; it uses the perceptual table when the profile lacks the intent's table.</returns>
    internal IccLutTransform Get(IccRendering rendering)
    {
        var table = SlotOf(rendering.Intent);
        var compensate = rendering.Compensate && rendering.Intent != IccIntent.AbsoluteColorimetric;
        var slot = (table * CompensationChoices) + (compensate ? 1 : 0);
        var existing = Volatile.Read(ref _transforms[slot]);
        if (existing is not null)
        {
            return existing;
        }

        lock (_gate)
        {
            if (_transforms[slot] is { } made)
            {
                return made;
            }

            var created = CreateTransform(rendering, table, compensate);
            Volatile.Write(ref _transforms[slot], created);
            return created;
        }
    }

    /// <summary>Gets the slot of an intent.</summary>
    /// <param name="intent">The intent.</param>
    /// <returns>0 for perceptual, 1 for relative or absolute colorimetric and 2 for saturation.</returns>
    private static int SlotOf(IccIntent intent) => intent switch
    {
        IccIntent.RelativeColorimetric or IccIntent.AbsoluteColorimetric => RelativeSlot,
        IccIntent.Saturation => SaturationSlot,
        _ => 0,
    };

    /// <summary>Gets the tag of an intent slot.</summary>
    /// <param name="slot">The slot.</param>
    /// <returns>The AToB tag signature.</returns>
    private static uint TagOf(int slot) => slot switch
    {
        RelativeSlot => RelativeTag,
        SaturationSlot => SaturationTag,
        _ => PerceptualTag,
    };

    /// <summary>Reads one AToB tag.</summary>
    /// <param name="tag">The tag signature.</param>
    /// <returns>The pipeline, or <see langword="null"/> when the profile has no valid tag.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IccPipeline? Parse(uint tag) => IccLutParser.Parse(IccProfile.FindTag(_profile, tag), _components, _labPcs);

    /// <summary>Creates the transform of an intent table and compensation choice.</summary>
    /// <param name="rendering">The rendering the transform serves.</param>
    /// <param name="table">The intent table's slot.</param>
    /// <param name="compensate">Whether the transform compensates for black points.</param>
    /// <returns>The transform.</returns>
    private IccLutTransform CreateTransform(IccRendering rendering, int table, bool compensate)
    {
        var pipeline = Parse(TagOf(table)) ?? Parse(PerceptualTag)!;
        var compensation = compensate ? Compensation(pipeline, table) : null;
        return new(this, pipeline, compensation, rendering);
    }

    /// <summary>
    /// Works out the map from the profile's black point to the output black point, detecting the source black point as
    /// ISO 18619 does: the fixed perceptual black of a v4 profile's perceptual and saturation tables, otherwise the
    /// lightness of the darkest colour the table gives.
    /// </summary>
    /// <param name="pipeline">The pipeline of the intent's table.</param>
    /// <param name="table">The intent table's slot.</param>
    /// <returns>The map, or <see langword="null"/> when the black points agree.</returns>
    private BlackPointCompensation? Compensation(IccPipeline pipeline, int table)
    {
        var source = _version >= FirstVersion4 && table != RelativeSlot ? PerceptualBlack : DarkestColorant(pipeline);

        // The output is a matrix/shaper profile, whose black is the darkest colorant: XYZ zero.
        return BlackPointCompensation.Between(source, default);
    }

    /// <summary>Finds the black point of a v2 profile: the lightness of its darkest colour, with no colour cast.</summary>
    /// <param name="pipeline">The profile's pipeline.</param>
    /// <returns>The black point in D50 XYZ; zero when the profile has no known darkest colour.</returns>
    private Float3 DarkestColorant(IccPipeline pipeline)
    {
        if (_darkest is null)
        {
            return default;
        }

        var darkest = pipeline.ToXyz(_darkest);
        var lightness = Math.Clamp(IccPcs.Lightness(darkest.Y), 0F, MaxBlackLightness);
        return IccPcs.LabToXyz(lightness, 0F, 0F);
    }
}
