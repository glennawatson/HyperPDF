// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Documents;

/// <summary>Which cells a table header cell labels.</summary>
public enum DocumentTableScope
{
    /// <summary>Not given, so a reader works it out from the table's shape.</summary>
    None = 0,

    /// <summary>The cells to its right in its row.</summary>
    Row = 1,

    /// <summary>The cells below it in its column.</summary>
    Column = 2,

    /// <summary>Both its row and its column.</summary>
    Both = 3,
}
