// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Documents;

/// <summary>Text search options.</summary>
[Flags]
public enum SearchOptions
{
    /// <summary>Case-insensitive, partial word matching.</summary>
    None = 0,

    /// <summary>Match letter case.</summary>
    MatchCase = 1 << 0,

    /// <summary>Match whole words only.</summary>
    WholeWord = 1 << 1,
}
