// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Matroska;

/// <summary>A place reading can start from: a block's offset and the start time of the cluster it is in.</summary>
/// <param name="Position">The file offset of the block, or -1 for none.</param>
/// <param name="ClusterSeconds">The start time of the cluster in seconds.</param>
internal readonly record struct MatroskaStart(long Position, double ClusterSeconds);
