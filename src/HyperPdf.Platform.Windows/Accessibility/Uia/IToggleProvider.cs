// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace HyperPdf.Platform.Windows.Accessibility.Uia;

/// <summary>The <c>IToggleProvider</c> interface, which switches an element on and off.</summary>
[GeneratedComInterface]
[Guid("56d00bd0-c4f4-433c-a836-1a52a57e0892")]
internal partial interface IToggleProvider
{
    /// <summary>Switches the element to its next state.</summary>
    void Toggle();

    /// <summary>Gets the element's state.</summary>
    /// <param name="state">Receives the state.</param>
    void GetToggleState(out ToggleState state);
}
