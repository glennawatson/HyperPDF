// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.ViewModels.Reading;

/// <summary>Whether loading the next page for reading aloud found one.</summary>
internal sealed class PageLoadState
{
    /// <summary>Gets or sets a value indicating whether a page with sentences to read was found; <see langword="false"/> at the end of the document.</summary>
    internal bool More { get; set; } = true;
}
