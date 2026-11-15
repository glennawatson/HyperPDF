// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace HyperPdf.Head.Windows.Accessibility;

/// <summary>The UI Automation interface of the root of a fragment: hit testing and the focused element.</summary>
[GeneratedComInterface]
[Guid("620ce2a5-ab8f-40a9-86cb-de3c75599b58")]
internal partial interface IRawElementProviderFragmentRoot
{
    /// <summary>Finds the element at a screen point.</summary>
    /// <param name="x">The x in screen coordinates.</param>
    /// <param name="y">The y in screen coordinates.</param>
    /// <param name="result">Receives the element, or null.</param>
    void ElementProviderFromPoint(double x, double y, out IRawElementProviderFragment? result);

    /// <summary>Gets the element that has keyboard focus.</summary>
    /// <param name="result">Receives the element, or null.</param>
    void GetFocus(out IRawElementProviderFragment? result);
}
