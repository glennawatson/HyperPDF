// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace HyperPdf.Head.Windows.Accessibility;

/// <summary>The UI Automation interface that places an element in the tree and gives it bounds.</summary>
[GeneratedComInterface]
[Guid("f7063da8-8359-439c-9297-bbc5299a7d87")]
internal partial interface IRawElementProviderFragment
{
    /// <summary>Moves through the tree.</summary>
    /// <param name="direction">0 parent, 1 next sibling, 2 previous sibling, 3 first child, 4 last child.</param>
    /// <param name="result">Receives the element, or null when there is none in that direction.</param>
    void Navigate(int direction, out IRawElementProviderFragment? result);

    /// <summary>Gets the runtime id that tells this element apart from the others in the window.</summary>
    /// <param name="runtimeId">Receives a SAFEARRAY of ints, or zero for the window root.</param>
    void GetRuntimeId(out nint runtimeId);

    /// <summary>Gets the bounds in screen coordinates.</summary>
    /// <param name="bounds">Receives the bounds.</param>
    void GetBoundingRectangle(out UiaRect bounds);

    /// <summary>Gets fragment roots embedded in this element.</summary>
    /// <param name="roots">Receives a SAFEARRAY, or zero when there are none.</param>
    void GetEmbeddedFragmentRoots(out nint roots);

    /// <summary>Moves keyboard focus to the element.</summary>
    void SetFocus();

    /// <summary>Gets the root of the fragment this element belongs to.</summary>
    /// <param name="root">Receives the root.</param>
    void GetFragmentRoot(out IRawElementProviderFragmentRoot? root);
}
