// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Codecs.Bits;

namespace HyperPdf.Codecs.H265;

/// <summary>The profile and level a stream says it needs.</summary>
/// <param name="Profile">The general_profile_idc: 1 is Main, 2 is Main 10, 3 is Main Still Picture, 4 and up are the extensions.</param>
/// <param name="Level">The general_level_idc: 30 times the level number.</param>
internal readonly record struct H265ProfileTier(int Profile, int Level)
{
    /// <summary>The bits of the profile space and tier flag, before the profile number.</summary>
    private const int SpaceTierBits = 3;

    /// <summary>The bits of the profile number.</summary>
    private const int ProfileBits = 5;

    /// <summary>The bits of the profile compatibility flags, source flags and constraint flags that follow the profile number.</summary>
    private const int ConstraintBits = 32 + 4 + 43 + 1;

    /// <summary>The bits of the level.</summary>
    private const int LevelBits = 8;

    /// <summary>The bits of the sub-layer profile information.</summary>
    private const int SubLayerProfileBits = SpaceTierBits + ProfileBits + ConstraintBits;

    /// <summary>The number of sub-layers the reserved bits pad up to.</summary>
    private const int MaxSubLayers = 8;

    /// <summary>The bits of the reserved padding for each missing sub-layer.</summary>
    private const int PaddingBits = 2;

    /// <summary>Reads profile_tier_level() with the profile present.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="maxSubLayersMinus1">The number of temporal sub-layers less one.</param>
    /// <returns>The general profile and level.</returns>
    internal static H265ProfileTier Read(ref MsbBitReader reader, int maxSubLayersMinus1)
    {
        reader.Skip(SpaceTierBits);
        var profile = (int)reader.Read(ProfileBits);
        BitHelpers.Skip(ref reader, ConstraintBits);
        var level = (int)reader.Read(LevelBits);
        SkipSubLayers(ref reader, maxSubLayersMinus1);
        return new(profile, level);
    }

    /// <summary>Skips the profile and level of the sub-layers.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="maxSubLayersMinus1">The number of temporal sub-layers less one.</param>
    private static void SkipSubLayers(ref MsbBitReader reader, int maxSubLayersMinus1)
    {
        var profilePresent = 0UL;
        var levelPresent = 0UL;
        for (var i = 0; i < maxSubLayersMinus1; i++)
        {
            profilePresent |= (ulong)reader.Read(1) << i;
            levelPresent |= (ulong)reader.Read(1) << i;
        }

        if (maxSubLayersMinus1 > 0)
        {
            BitHelpers.Skip(ref reader, (MaxSubLayers - maxSubLayersMinus1) * PaddingBits);
        }

        for (var i = 0; i < maxSubLayersMinus1; i++)
        {
            if (((profilePresent >> i) & 1) != 0)
            {
                BitHelpers.Skip(ref reader, SubLayerProfileBits);
            }

            if (((levelPresent >> i) & 1) != 0)
            {
                reader.Skip(LevelBits);
            }
        }
    }
}
