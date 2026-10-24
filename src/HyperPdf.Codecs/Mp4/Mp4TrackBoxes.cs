// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Mp4;

/// <summary>The boxes of a track box that describe the track and its samples.</summary>
/// <param name="Header">The track header box.</param>
/// <param name="MediaHeader">The media header box.</param>
/// <param name="Handler">The handler box.</param>
/// <param name="Table">The sample table box.</param>
/// <param name="Description">The sample description box.</param>
internal readonly record struct Mp4TrackBoxes(Mp4Box Header, Mp4Box MediaHeader, Mp4Box Handler, Mp4Box Table, Mp4Box Description);
