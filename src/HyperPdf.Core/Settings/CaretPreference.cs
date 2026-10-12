// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Settings;

/// <summary>Whether the text cursor blinks.</summary>
public enum CaretPreference
{
    /// <summary>Follow the desktop's cursor blink setting.</summary>
    FollowDesktop = 0,

    /// <summary>A steady cursor that never blinks.</summary>
    Steady = 1,

    /// <summary>A blinking cursor.</summary>
    Blinking = 2,
}
