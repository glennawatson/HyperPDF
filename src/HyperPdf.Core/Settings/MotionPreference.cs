// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Settings;

/// <summary>Whether interface transitions play.</summary>
public enum MotionPreference
{
    /// <summary>Follow the desktop's animation speed (KDE: instant means none).</summary>
    FollowDesktop = 0,

    /// <summary>No transitions.</summary>
    Reduced = 1,

    /// <summary>Short functional transitions.</summary>
    Normal = 2,
}
