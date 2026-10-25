// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace HyperPdf.Platform.Windows.Accessibility.Uia;

/// <summary>The <c>IRawElementProviderSimple</c> interface, which gives UI Automation an element's properties and patterns.</summary>
[GeneratedComInterface]
[Guid("d6dd68d1-86fd-4332-8666-9abedea2d24c")]
internal partial interface IRawElementProviderSimple
{
    /// <summary>Gets how UI Automation treats the provider.</summary>
    /// <param name="options">Receives the options.</param>
    void GetProviderOptions(out ProviderOptions options);

    /// <summary>Gets the object that implements a pattern.</summary>
    /// <param name="patternId">The pattern, a <see cref="UiaPatternId"/>.</param>
    /// <returns>The <c>IUnknown</c> pointer of the object, counted for the caller, or null when the element lacks the pattern.</returns>
    nint GetPatternProvider(int patternId);

    /// <summary>Gets the value of a property.</summary>
    /// <param name="propertyId">The property, a <see cref="UiaPropertyId"/>.</param>
    /// <returns>The value, or an empty variant when the provider has no value for it.</returns>
    ComVariant GetPropertyValue(int propertyId);

    /// <summary>Gets the provider that UI Automation makes for the window that holds the element.</summary>
    /// <returns>The <c>IRawElementProviderSimple</c> pointer, counted for the caller, or null for an element that is not a window's root.</returns>
    nint GetHostRawElementProvider();
}
