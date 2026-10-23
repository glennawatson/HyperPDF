// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace HyperPdf.Platform.Windows.Accessibility.Uia;

/// <summary>The <c>IRawElementProviderFragmentRoot</c> interface, which a window's root element adds to look up elements.</summary>
[GeneratedComInterface]
[Guid("620ce2a5-ab8f-40a9-86cb-de3c75599b58")]
internal partial interface IRawElementProviderFragmentRoot
{
    /// <summary>Finds the element at a point.</summary>
    /// <param name="x">Pixels from the left of the screen.</param>
    /// <param name="y">Pixels from the top of the screen.</param>
    /// <returns>The element, or <see langword="null"/> when there is none.</returns>
    IRawElementProviderFragment? ElementProviderFromPoint(double x, double y);

    /// <summary>Finds the element with the keyboard focus.</summary>
    /// <returns>The element, or <see langword="null"/> when none in the window has it.</returns>
    IRawElementProviderFragment? GetFocus();
}
