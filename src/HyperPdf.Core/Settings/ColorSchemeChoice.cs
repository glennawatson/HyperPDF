// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Settings;

/// <summary>The colour scheme the window uses.</summary>
public enum ColorSchemeChoice
{
    /// <summary>The desktop's colour scheme (KDE <c>kdeglobals</c>), falling back to Calm.</summary>
    FollowDesktop = 0,

    /// <summary>Low contrast warm neutrals with muted accents.</summary>
    Calm = 1,

    /// <summary>Maximum legibility.</summary>
    HighContrast = 2,

    /// <summary>A dark neutral scheme.</summary>
    Dark = 3,

    /// <summary>A light neutral scheme.</summary>
    Light = 4,
}
