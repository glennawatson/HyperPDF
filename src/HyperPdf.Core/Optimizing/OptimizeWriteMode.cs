// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Optimizing;

/// <summary>How an optimised copy was written.</summary>
public enum OptimizeWriteMode
{
    /// <summary>The whole file was rewritten.</summary>
    Rewritten = 0,

    /// <summary>The original bytes were kept and only additions were appended, so signatures stay valid.</summary>
    Incremental = 1,

    /// <summary>Nothing could change safely, so the original bytes were copied.</summary>
    Copied = 2,
}
