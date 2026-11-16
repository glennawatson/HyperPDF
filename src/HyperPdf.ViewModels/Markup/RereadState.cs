// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.ViewModels.Markup;

/// <summary>What the attempts to find an edited object on a page that was read again have found so far.</summary>
internal sealed class RereadState
{
    /// <summary>Gets or sets the index of the object in the page's objects, or -1 while it has not been found.</summary>
    internal int Again { get; set; } = -1;

    /// <summary>Gets or sets a value indicating whether the object was meant to be gone, so it is not looked for.</summary>
    internal bool Gone { get; set; }

    /// <summary>Gets or sets a value indicating whether the attempts are over.</summary>
    internal bool Finished { get; set; }
}
