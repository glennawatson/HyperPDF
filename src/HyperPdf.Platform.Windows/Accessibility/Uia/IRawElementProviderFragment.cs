// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace HyperPdf.Platform.Windows.Accessibility.Uia;

/// <summary>The <c>IRawElementProviderFragment</c> interface, which puts an element in the tree.</summary>
[GeneratedComInterface]
[Guid("f7063da8-8359-439c-9297-bbc5299a7d87")]
internal partial interface IRawElementProviderFragment
{
    /// <summary>Moves through the tree.</summary>
    /// <param name="direction">The direction.</param>
    /// <returns>The element there, or <see langword="null"/> when there is none.</returns>
    IRawElementProviderFragment? Navigate(NavigateDirection direction);

    /// <summary>Gets the element's runtime identifier.</summary>
    /// <returns>A <c>SAFEARRAY</c> of integers, or null to let UI Automation use the window's.</returns>
    nint GetRuntimeId();

    /// <summary>Gets where the element is on the screen.</summary>
    /// <returns>The rectangle.</returns>
    UiaRect GetBoundingRectangle();

    /// <summary>Gets the roots of other windows hosted inside the element.</summary>
    /// <returns>A <c>SAFEARRAY</c> of providers, or null when there are none.</returns>
    nint GetEmbeddedFragmentRoots();

    /// <summary>Moves the keyboard focus to the element.</summary>
    void SetFocus();

    /// <summary>Gets the root of the tree the element is in.</summary>
    /// <returns>The root.</returns>
    IRawElementProviderFragmentRoot? GetFragmentRoot();
}
