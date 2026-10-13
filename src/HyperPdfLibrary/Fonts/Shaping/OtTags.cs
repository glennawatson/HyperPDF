// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts.Shaping;

/// <summary>The script, language and feature tags the shaper selects (OpenType, "Registered features" and "Script tags").</summary>
internal static class OtTags
{
    /// <summary>The default script, DFLT.</summary>
    internal static readonly uint DefaultScript = OtTag.Of("DFLT");

    /// <summary>The Latin script, which fonts commonly use for any script they have no entry for.</summary>
    internal static readonly uint Latin = OtTag.Of("latn");

    /// <summary>The ccmp feature: glyph composition and decomposition.</summary>
    internal static readonly uint Ccmp = OtTag.Of("ccmp");

    /// <summary>The locl feature: localized forms.</summary>
    internal static readonly uint Locl = OtTag.Of("locl");

    /// <summary>The rtlm feature: right-to-left mirrored forms.</summary>
    internal static readonly uint Rtlm = OtTag.Of("rtlm");

    /// <summary>The rlig feature: required ligatures.</summary>
    internal static readonly uint Rlig = OtTag.Of("rlig");

    /// <summary>The calt feature: contextual alternates.</summary>
    internal static readonly uint Calt = OtTag.Of("calt");

    /// <summary>The rclt feature: required contextual alternates.</summary>
    internal static readonly uint Rclt = OtTag.Of("rclt");

    /// <summary>The liga feature: standard ligatures.</summary>
    internal static readonly uint Liga = OtTag.Of("liga");

    /// <summary>The clig feature: contextual ligatures.</summary>
    internal static readonly uint Clig = OtTag.Of("clig");

    /// <summary>The isol feature: isolated forms.</summary>
    internal static readonly uint Isol = OtTag.Of("isol");

    /// <summary>The fina feature: terminal forms.</summary>
    internal static readonly uint Fina = OtTag.Of("fina");

    /// <summary>The fin2 feature: terminal forms 2.</summary>
    internal static readonly uint Fin2 = OtTag.Of("fin2");

    /// <summary>The fin3 feature: terminal forms 3.</summary>
    internal static readonly uint Fin3 = OtTag.Of("fin3");

    /// <summary>The medi feature: medial forms.</summary>
    internal static readonly uint Medi = OtTag.Of("medi");

    /// <summary>The med2 feature: medial forms 2.</summary>
    internal static readonly uint Med2 = OtTag.Of("med2");

    /// <summary>The init feature: initial forms.</summary>
    internal static readonly uint Init = OtTag.Of("init");

    /// <summary>The vert feature: vertical alternates.</summary>
    internal static readonly uint Vert = OtTag.Of("vert");

    /// <summary>The vrt2 feature: vertical alternates and rotation.</summary>
    internal static readonly uint Vrt2 = OtTag.Of("vrt2");

    /// <summary>The kern feature: kerning.</summary>
    internal static readonly uint Kern = OtTag.Of("kern");

    /// <summary>The mark feature: mark positioning.</summary>
    internal static readonly uint Mark = OtTag.Of("mark");

    /// <summary>The mkmk feature: mark-to-mark positioning.</summary>
    internal static readonly uint Mkmk = OtTag.Of("mkmk");

    /// <summary>The curs feature: cursive positioning.</summary>
    internal static readonly uint Curs = OtTag.Of("curs");

    /// <summary>The dist feature: distances.</summary>
    internal static readonly uint Dist = OtTag.Of("dist");

    /// <summary>The abvm feature: above-base mark positioning.</summary>
    internal static readonly uint Abvm = OtTag.Of("abvm");

    /// <summary>The blwm feature: below-base mark positioning.</summary>
    internal static readonly uint Blwm = OtTag.Of("blwm");

    /// <summary>The nukt feature: nukta forms.</summary>
    internal static readonly uint Nukt = OtTag.Of("nukt");

    /// <summary>The akhn feature: akhand ligatures.</summary>
    internal static readonly uint Akhn = OtTag.Of("akhn");

    /// <summary>The rphf feature: reph forms.</summary>
    internal static readonly uint Rphf = OtTag.Of("rphf");

    /// <summary>The rkrf feature: rakaar forms.</summary>
    internal static readonly uint Rkrf = OtTag.Of("rkrf");

    /// <summary>The pref feature: pre-base forms.</summary>
    internal static readonly uint Pref = OtTag.Of("pref");

    /// <summary>The blwf feature: below-base forms.</summary>
    internal static readonly uint Blwf = OtTag.Of("blwf");

    /// <summary>The abvf feature: above-base forms.</summary>
    internal static readonly uint Abvf = OtTag.Of("abvf");

    /// <summary>The half feature: half forms.</summary>
    internal static readonly uint Half = OtTag.Of("half");

    /// <summary>The pstf feature: post-base forms.</summary>
    internal static readonly uint Pstf = OtTag.Of("pstf");

    /// <summary>The vatu feature: vattu variants.</summary>
    internal static readonly uint Vatu = OtTag.Of("vatu");

    /// <summary>The cjct feature: conjunct forms.</summary>
    internal static readonly uint Cjct = OtTag.Of("cjct");

    /// <summary>The pres feature: pre-base substitutions.</summary>
    internal static readonly uint Pres = OtTag.Of("pres");

    /// <summary>The abvs feature: above-base substitutions.</summary>
    internal static readonly uint Abvs = OtTag.Of("abvs");

    /// <summary>The blws feature: below-base substitutions.</summary>
    internal static readonly uint Blws = OtTag.Of("blws");

    /// <summary>The psts feature: post-base substitutions.</summary>
    internal static readonly uint Psts = OtTag.Of("psts");

    /// <summary>The haln feature: halant forms.</summary>
    internal static readonly uint Haln = OtTag.Of("haln");
}
