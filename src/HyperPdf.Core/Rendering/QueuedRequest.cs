// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Rendering;

/// <summary>A tile waiting in the work queue. The request itself stays in the scheduler's pending map.</summary>
/// <param name="Key">The tile the entry stands for.</param>
/// <param name="Stamp">The ordering key: the priority above the arrival sequence, so smaller stamps are served first.</param>
[DebuggerDisplay("QueuedRequest: {Key} stamp {Stamp}")]
internal readonly record struct QueuedRequest(TileKey Key, long Stamp);
