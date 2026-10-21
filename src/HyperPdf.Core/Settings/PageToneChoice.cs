// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Settings;

/// <summary>How page paper and ink are coloured.</summary>
public enum PageToneChoice
{
    /// <summary>Pick the tone that suits the colour scheme.</summary>
    MatchScheme = 0,

    /// <summary>Pages exactly as the document draws them.</summary>
    White = 1,

    /// <summary>Warm off-white paper with dark ink.</summary>
    SoftPaper = 2,

    /// <summary>Warm grey paper with soft light ink.</summary>
    CalmNight = 3,

    /// <summary>Dark paper with light ink.</summary>
    Dark = 4,

    /// <summary>White or dark pages following the desktop appearance.</summary>
    FollowDesktop = 5,

    /// <summary>Yellow ink on black paper.</summary>
    YellowOnBlack = 6,

    /// <summary>Green ink on black paper.</summary>
    GreenOnBlack = 7,

    /// <summary>White ink on black paper.</summary>
    WhiteOnBlack = 8,

    /// <summary>The paper and ink colours the user chose.</summary>
    Custom = 9,
}
