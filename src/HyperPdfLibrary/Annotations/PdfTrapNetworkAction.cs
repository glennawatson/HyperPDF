// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Annotations;

/// <summary>What a save or page copy did with a page's trap network.</summary>
public enum PdfTrapNetworkAction
{
    /// <summary>Nothing was done.</summary>
    None = 0,

    /// <summary>The network no longer matched its page or could not be checked, so it was removed.</summary>
    Removed = 1,

    /// <summary>The network still matched its page, and its flags, position or appearance state were fixed.</summary>
    Repaired = 2,

    /// <summary>
    /// The network needed to be removed or fixed, but the page is written inside its parent's <c>/Kids</c> array rather
    /// than as an object of its own, so it was left as it was.
    /// </summary>
    Unchanged = 3,

    /// <summary>The network no longer matched its page or could not be checked, so a page copy did not copy it.</summary>
    LeftOut = 4,
}
