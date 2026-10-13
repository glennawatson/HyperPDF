// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Rendering;

/// <summary>A render request waiting in the work queue.</summary>
/// <param name="Request">The request.</param>
/// <param name="Stamp">The ordering key: the priority above the arrival sequence, so smaller stamps are served first.</param>
[DebuggerDisplay("QueuedRequest: {Request} stamp {Stamp}")]
internal readonly record struct QueuedRequest(RenderRequest Request, long Stamp);
