// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Accessibility;

/// <summary>The kind of a parent-child structure rule in veraPDF's tagged PDF profiles.</summary>
internal enum StandardsContainmentKind
{
    /// <summary>The parent type may not hold the child type.</summary>
    Forbidden = 0,

    /// <summary>The parent type may hold the child type at most once.</summary>
    AtMostOnce = 1,
}
