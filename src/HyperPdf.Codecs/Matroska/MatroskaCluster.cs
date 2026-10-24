// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Matroska;

/// <summary>A cluster found when the file was opened: where it starts and when.</summary>
/// <param name="Offset">The file offset of the cluster's header.</param>
/// <param name="Seconds">The cluster's start time in seconds.</param>
internal readonly record struct MatroskaCluster(long Offset, double Seconds);
