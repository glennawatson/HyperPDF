// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Rendering;

/// <summary>Whether lines are fitted to whole pixels so they have an even thickness.</summary>
public enum StrokeAdjustmentChoice
{
    /// <summary>Lines are never fitted.</summary>
    Off = 0,

    /// <summary>Lines are fitted where the page asks for it.</summary>
    FollowPage = 1,

    /// <summary>Lines are always fitted.</summary>
    Always = 2,
}
