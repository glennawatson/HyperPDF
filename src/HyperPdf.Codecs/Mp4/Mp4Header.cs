// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Mp4;

/// <summary>The time scale and duration of a movie or media header.</summary>
/// <param name="Scale">The ticks in a second.</param>
/// <param name="Duration">The duration in ticks.</param>
internal readonly record struct Mp4Header(long Scale, long Duration);
