// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Settings;

/// <summary>The page colour of Focus Mode.</summary>
public enum FocusPageColour
{
    /// <summary>Follow the app's page colour.</summary>
    MatchPages = 0,

    /// <summary>Warm, soft paper with dark grey text.</summary>
    SoftPaper = 1,

    /// <summary>A muted sage tint, easy on tired eyes.</summary>
    Sage = 2,

    /// <summary>Dark grey with warm light text.</summary>
    CalmNight = 3,

    /// <summary>White with black text.</summary>
    White = 4,
}
