// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Matroska;

/// <summary>What the cluster and block group around a block say about its frames.</summary>
/// <param name="ClusterSeconds">The cluster's start time in seconds.</param>
/// <param name="Scale">The seconds in a timestamp unit.</param>
/// <param name="Duration">The block's length in seconds when its block group says, otherwise 0.</param>
/// <param name="IsKey">Whether the block is a key block, for a block that has no key flag of its own.</param>
internal readonly record struct MatroskaBlockTiming(double ClusterSeconds, double Scale, double Duration, bool IsKey);
