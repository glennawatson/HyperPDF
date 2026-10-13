// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Filters;

/// <summary>
/// The part of an element pool its owner needs without knowing the element type: pools form a chain that the owner walks
/// to empty them all, so the owner keeps no table or callback list.
/// </summary>
internal abstract class ScratchPoolBase
{
    /// <summary>Gets or sets the pool made before this one, or <see langword="null"/>; set before the pool is published and never changed.</summary>
    internal ScratchPoolBase? Next { get; set; }

    /// <summary>Releases every kept array.</summary>
    internal abstract void Trim();
}
