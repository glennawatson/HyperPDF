// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace HyperPdf.Head.Windows.Accessibility;

/// <summary>The UI Automation interface every element implements: its options, patterns and properties.</summary>
[GeneratedComInterface]
[Guid("d6dd68d1-86fd-4332-8666-9abedea2d24c")]
internal partial interface IRawElementProviderSimple
{
    /// <summary>Gets the provider options, such as server side provider.</summary>
    /// <param name="options">Receives the option flags.</param>
    void GetProviderOptions(out int options);

    /// <summary>Gets a control pattern provider.</summary>
    /// <param name="patternId">The pattern id.</param>
    /// <param name="provider">Receives the provider, or zero when the pattern is not supported.</param>
    void GetPatternProvider(int patternId, out nint provider);

    /// <summary>Gets a property value.</summary>
    /// <param name="propertyId">The property id.</param>
    /// <param name="value">Receives the value, or an empty variant when the property is not supplied.</param>
    void GetPropertyValue(int propertyId, out UiaVariant value);

    /// <summary>Gets the provider of the window that hosts this element.</summary>
    /// <param name="provider">Receives the host provider, or zero when this element is not a window root.</param>
    void GetHostRawElementProvider(out nint provider);
}
