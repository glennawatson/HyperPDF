// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Printing;

/// <summary>The edge used to turn a sheet printed on both sides.</summary>
public enum DuplexBinding
{
    /// <summary>Turn along the sheet's long edge.</summary>
    LongEdge = 0,

    /// <summary>Turn along the sheet's short edge.</summary>
    ShortEdge = 1,
}
