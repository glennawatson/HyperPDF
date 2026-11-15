// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Viewer.Chrome;

/// <summary>The state a page picture was asked for in. A request made in the same state is still live and is not made again.</summary>
/// <param name="Generation">The generation of the tab's thumbnail requests.</param>
/// <param name="Version">The version of the page pictures in the cache.</param>
[DebuggerDisplay("ThumbnailAsk: generation {Generation}, version {Version}")]
internal readonly record struct ThumbnailAsk(int Generation, int Version);
