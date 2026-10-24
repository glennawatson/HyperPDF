// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Matroska;

/// <summary>A cluster found when the file was scanned, with its start time still in timestamp units.</summary>
/// <param name="Offset">The file offset of the cluster's header.</param>
/// <param name="Ticks">The cluster's start time in timestamp units.</param>
internal readonly record struct MatroskaClusterTicks(long Offset, ulong Ticks);
