// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Media;

/// <summary>What a media player shows at one moment.</summary>
/// <param name="State">Where the player is in its life.</param>
/// <param name="Position">How far into the file the player is.</param>
/// <param name="Duration">How long the file plays, or zero when it does not say.</param>
/// <param name="Caption">The words of the caption showing now, or an empty string.</param>
/// <param name="FrameVersion">A number that grows with each new picture, so a view knows when to draw again.</param>
[DebuggerDisplay("MediaSnapshot: {State} {Position}")]
public readonly record struct MediaSnapshot(MediaPlayerState State, TimeSpan Position, TimeSpan Duration, string Caption, int FrameVersion);
