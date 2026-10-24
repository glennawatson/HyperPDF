// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Mp4;

/// <summary>Where a track's media starts, from its edit list.</summary>
/// <param name="MediaStart">The media time in ticks at which the track starts.</param>
/// <param name="Shift">The seconds to add to a media time to get the time in the file.</param>
internal readonly record struct Mp4Edit(long MediaStart, double Shift);
