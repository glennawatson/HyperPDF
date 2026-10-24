// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace HyperPdf.Platform.Windows.Accessibility.Uia;

/// <summary>The <c>ISelectionItemProvider</c> interface, which selects one item of a list or a set of tabs.</summary>
[GeneratedComInterface]
[Guid("2acad808-b2d4-452d-a407-91ff1ad167b2")]
internal partial interface ISelectionItemProvider
{
    /// <summary>Selects the item and clears the others.</summary>
    void Select();

    /// <summary>Adds the item to the selection.</summary>
    void AddToSelection();

    /// <summary>Removes the item from the selection.</summary>
    void RemoveFromSelection();

    /// <summary>Gets whether the item is selected.</summary>
    /// <returns>Whether it is.</returns>
    [return: MarshalAs(UnmanagedType.Bool)]
    bool GetIsSelected();

    /// <summary>Gets the element that holds the items.</summary>
    /// <returns>The container, or <see langword="null"/>.</returns>
    IRawElementProviderSimple? GetSelectionContainer();
}
