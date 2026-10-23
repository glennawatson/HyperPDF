// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace HyperPdf.Platform.Windows.Accessibility.Uia;

/// <summary>The <c>IValueProvider</c> interface, which gives an element's text value.</summary>
[GeneratedComInterface]
[Guid("c7935180-6fb3-4201-b174-7df73adbf64a")]
internal partial interface IValueProvider
{
    /// <summary>Sets the value.</summary>
    /// <param name="value">The new value.</param>
    void SetValue([MarshalAs(UnmanagedType.LPWStr)] string value);

    /// <summary>Gets the value.</summary>
    /// <returns>The value.</returns>
    [return: MarshalAs(UnmanagedType.BStr)]
    string GetValue();

    /// <summary>Gets whether the value cannot be changed.</summary>
    /// <returns>Whether it is read only.</returns>
    [return: MarshalAs(UnmanagedType.Bool)]
    bool GetIsReadOnly();
}
