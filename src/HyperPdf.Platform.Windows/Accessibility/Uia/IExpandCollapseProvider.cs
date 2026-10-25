// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace HyperPdf.Platform.Windows.Accessibility.Uia;

/// <summary>The <c>IExpandCollapseProvider</c> interface, which opens and closes an element.</summary>
[GeneratedComInterface]
[Guid("d847d3a5-cab0-4a98-8c32-ecb45c59ad24")]
internal partial interface IExpandCollapseProvider
{
    /// <summary>Opens the element.</summary>
    void Expand();

    /// <summary>Closes the element.</summary>
    void Collapse();

    /// <summary>Gets whether the element is open.</summary>
    /// <param name="state">Receives the state.</param>
    void GetExpandCollapseState(out ExpandCollapseState state);
}
