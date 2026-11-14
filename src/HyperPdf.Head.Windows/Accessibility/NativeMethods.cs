// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Head.Windows.Accessibility;

/// <summary>Source generated entry points of UI Automation, OLE automation and user32 that the accessibility bridge needs.</summary>
internal static unsafe partial class NativeMethods
{
    /// <summary>The UI Automation core library.</summary>
    private const string UiaCore = "uiautomationcore";

    /// <summary>The OLE automation library, which makes SAFEARRAYs.</summary>
    private const string OleAut = "oleaut32";

    /// <summary>The user32 library.</summary>
    private const string User32 = "user32";

    /// <summary>Native <c>UiaReturnRawElementProvider</c>, which answers WM_GETOBJECT.</summary>
    /// <param name="window">The window.</param>
    /// <param name="primary">The WM_GETOBJECT wParam.</param>
    /// <param name="secondary">The WM_GETOBJECT lParam.</param>
    /// <param name="provider">The IRawElementProviderSimple of the window root, or zero.</param>
    /// <returns>The result for the window procedure to return.</returns>
    [LibraryImport(UiaCore, EntryPoint = "UiaReturnRawElementProvider")]
    internal static partial nint ReturnRawElementProvider(nint window, nuint primary, nint secondary, nint provider);

    /// <summary>Native <c>UiaHostProviderFromHwnd</c>.</summary>
    /// <param name="window">The window.</param>
    /// <param name="provider">Receives the host provider.</param>
    /// <returns>An HRESULT.</returns>
    [LibraryImport(UiaCore, EntryPoint = "UiaHostProviderFromHwnd")]
    internal static partial int HostProviderFromWindow(nint window, out nint provider);

    /// <summary>Native <c>UiaClientsAreListening</c>.</summary>
    /// <returns>Nonzero when an assistive technology is using UI Automation.</returns>
    [LibraryImport(UiaCore, EntryPoint = "UiaClientsAreListening")]
    internal static partial int ClientsAreListening();

    /// <summary>Native <c>UiaRaiseAutomationEvent</c>.</summary>
    /// <param name="provider">The element's IRawElementProviderSimple.</param>
    /// <param name="eventId">The event id.</param>
    /// <returns>An HRESULT.</returns>
    [LibraryImport(UiaCore, EntryPoint = "UiaRaiseAutomationEvent")]
    internal static partial int RaiseAutomationEvent(nint provider, int eventId);

    /// <summary>Native <c>UiaRaiseStructureChangedEvent</c>.</summary>
    /// <param name="provider">The element's IRawElementProviderSimple.</param>
    /// <param name="changeType">The structure change type.</param>
    /// <param name="runtimeId">The runtime ids of the changed children, or null.</param>
    /// <param name="runtimeIdCount">The number of ints in the runtime ids.</param>
    /// <returns>An HRESULT.</returns>
    [LibraryImport(UiaCore, EntryPoint = "UiaRaiseStructureChangedEvent")]
    internal static partial int RaiseStructureChangedEvent(nint provider, int changeType, nint runtimeId, int runtimeIdCount);

    /// <summary>Native <c>SafeArrayCreateVector</c>.</summary>
    /// <param name="type">The element VARTYPE.</param>
    /// <param name="lowerBound">The lower bound.</param>
    /// <param name="count">The element count.</param>
    /// <returns>The SAFEARRAY, or zero on failure.</returns>
    [LibraryImport(OleAut, EntryPoint = "SafeArrayCreateVector")]
    internal static partial nint CreateVector(ushort type, int lowerBound, uint count);

    /// <summary>Native <c>SafeArrayPutElement</c>.</summary>
    /// <param name="array">The SAFEARRAY.</param>
    /// <param name="index">The index, as a one element array.</param>
    /// <param name="value">The value.</param>
    /// <returns>An HRESULT.</returns>
    [LibraryImport(OleAut, EntryPoint = "SafeArrayPutElement")]
    internal static partial int PutElement(nint array, int* index, int* value);

    /// <summary>Native <c>ClientToScreen</c>.</summary>
    /// <param name="window">The window.</param>
    /// <param name="point">The POINT, two ints, converted in place.</param>
    /// <returns>Nonzero on success.</returns>
    [LibraryImport(User32, EntryPoint = "ClientToScreen")]
    internal static partial int ClientToScreen(nint window, int* point);
}
