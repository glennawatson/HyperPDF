// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Microsoft.Win32.SafeHandles;

namespace HyperPdf.Platform.Windows;

/// <summary>Source generated entry points of the Windows APIs the integration uses, all part of Windows itself.</summary>
internal static unsafe partial class NativeMethods
{
    /// <summary>Native <c>CoInitializeEx</c>.</summary>
    /// <param name="reserved">Must be null.</param>
    /// <param name="flags">The threading model.</param>
    /// <returns>An HRESULT.</returns>
    [LibraryImport("ole32.dll", EntryPoint = "CoInitializeEx")]
    internal static partial int CoInitializeEx(nint reserved, uint flags);

    /// <summary>Native <c>CoUninitialize</c>.</summary>
    [LibraryImport("ole32.dll", EntryPoint = "CoUninitialize")]
    internal static partial void CoUninitialize();

    /// <summary>Native <c>CoCreateInstance</c>.</summary>
    /// <param name="classId">The class.</param>
    /// <param name="outer">The aggregating object, or null.</param>
    /// <param name="context">Where the object runs.</param>
    /// <param name="interfaceId">The interface wanted.</param>
    /// <param name="instance">Receives the interface.</param>
    /// <returns>An HRESULT.</returns>
    [LibraryImport("ole32.dll", EntryPoint = "CoCreateInstance")]
    internal static partial int CoCreateInstance(Guid* classId, void* outer, uint context, Guid* interfaceId, void** instance);

    /// <summary>Native <c>ILCreateFromPathW</c>.</summary>
    /// <param name="path">The path.</param>
    /// <returns>The item identifier list, or null.</returns>
    [LibraryImport("shell32.dll", EntryPoint = "ILCreateFromPathW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint ILCreateFromPath(string path);

    /// <summary>Native <c>ILFree</c>.</summary>
    /// <param name="list">The item identifier list.</param>
    [LibraryImport("shell32.dll", EntryPoint = "ILFree")]
    internal static partial void ILFree(nint list);

    /// <summary>Native <c>SHOpenFolderAndSelectItems</c>.</summary>
    /// <param name="folder">The folder's item identifier list.</param>
    /// <param name="count">The number of items to select.</param>
    /// <param name="items">The items, or null.</param>
    /// <param name="flags">The flags.</param>
    /// <returns>An HRESULT.</returns>
    [LibraryImport("shell32.dll", EntryPoint = "SHOpenFolderAndSelectItems")]
    internal static partial int SHOpenFolderAndSelectItems(nint folder, uint count, nint items, uint flags);

    /// <summary>Native <c>SHAddToRecentDocs</c>.</summary>
    /// <param name="flags">How the item is given; <c>SHARD_PATHW</c> for a path.</param>
    /// <param name="path">The path.</param>
    [LibraryImport("shell32.dll", EntryPoint = "SHAddToRecentDocs", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial void SHAddToRecentDocs(uint flags, string path);

    /// <summary>Native <c>AllowSetForegroundWindow</c>.</summary>
    /// <param name="processId">The process allowed, or <c>ASFW_ANY</c>.</param>
    /// <returns>Nonzero on success.</returns>
    [LibraryImport("user32.dll", EntryPoint = "AllowSetForegroundWindow")]
    internal static partial int AllowSetForegroundWindow(uint processId);

    /// <summary>Native <c>GetForegroundWindow</c>.</summary>
    /// <returns>The foreground window.</returns>
    [LibraryImport("user32.dll", EntryPoint = "GetForegroundWindow")]
    internal static partial nint GetForegroundWindow();

    /// <summary>Native <c>GetCaretBlinkTime</c>.</summary>
    /// <returns>The blink interval in milliseconds, or <c>INFINITE</c> for none.</returns>
    [LibraryImport("user32.dll", EntryPoint = "GetCaretBlinkTime")]
    internal static partial uint GetCaretBlinkTime();

    /// <summary>Native <c>SystemParametersInfoW</c>.</summary>
    /// <param name="action">The parameter.</param>
    /// <param name="value">A parameter-specific value.</param>
    /// <param name="data">A parameter-specific buffer.</param>
    /// <param name="flags">Update flags.</param>
    /// <returns>Nonzero on success.</returns>
    [LibraryImport("user32.dll", EntryPoint = "SystemParametersInfoW")]
    internal static partial int SystemParametersInfo(uint action, uint value, void* data, uint flags);

    /// <summary>Native <c>SystemParametersInfoW</c> for a setting that is a Boolean.</summary>
    /// <param name="action">The setting, such as <c>SPI_GETSCREENREADER</c>.</param>
    /// <param name="parameter">A number the setting may use.</param>
    /// <param name="value">Receives the setting's value.</param>
    /// <param name="flags">Whether to save a change; 0 when only reading.</param>
    /// <returns>Nonzero on success.</returns>
    [LibraryImport("user32.dll", EntryPoint = "SystemParametersInfoW")]
    internal static partial int SystemParametersInfo(uint action, uint parameter, out int value, uint flags);

    /// <summary>Native <c>RegNotifyChangeKeyValue</c>.</summary>
    /// <param name="key">The key.</param>
    /// <param name="watchSubtree">Whether to watch subkeys.</param>
    /// <param name="filter">The changes reported.</param>
    /// <param name="changed">The event signalled on a change.</param>
    /// <param name="asynchronous">Whether to return at once.</param>
    /// <returns>An error code.</returns>
    [LibraryImport("advapi32.dll", EntryPoint = "RegNotifyChangeKeyValue")]
    internal static partial int RegNotifyChangeKeyValue(SafeRegistryHandle key, int watchSubtree, uint filter, SafeWaitHandle changed, int asynchronous);

    /// <summary>Native <c>EnumPrintersW</c>.</summary>
    /// <param name="flags">Which printers.</param>
    /// <param name="name">The server, or null.</param>
    /// <param name="level">The structure level.</param>
    /// <param name="buffer">The buffer, or null to ask for the size.</param>
    /// <param name="size">The buffer size.</param>
    /// <param name="needed">Receives the size needed.</param>
    /// <param name="returned">Receives the printer count.</param>
    /// <returns>Nonzero on success.</returns>
    [LibraryImport("winspool.drv", EntryPoint = "EnumPrintersW")]
    internal static partial int EnumPrinters(uint flags, char* name, uint level, byte* buffer, uint size, out uint needed, out uint returned);

    /// <summary>Native <c>GetDefaultPrinterW</c>.</summary>
    /// <param name="buffer">Receives the name.</param>
    /// <param name="size">The buffer size in characters; receives the size needed.</param>
    /// <returns>Nonzero on success.</returns>
    [LibraryImport("winspool.drv", EntryPoint = "GetDefaultPrinterW")]
    internal static partial int GetDefaultPrinter(char* buffer, ref uint size);

    /// <summary>Native <c>OpenPrinterW</c>.</summary>
    /// <param name="name">The printer.</param>
    /// <param name="printer">Receives the handle.</param>
    /// <param name="defaults">The defaults, or null.</param>
    /// <returns>Nonzero on success.</returns>
    [LibraryImport("winspool.drv", EntryPoint = "OpenPrinterW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial int OpenPrinter(string name, out nint printer, void* defaults);

    /// <summary>Native <c>ClosePrinter</c>.</summary>
    /// <param name="printer">The handle.</param>
    /// <returns>Nonzero on success.</returns>
    [LibraryImport("winspool.drv", EntryPoint = "ClosePrinter")]
    internal static partial int ClosePrinter(nint printer);

    /// <summary>Native <c>DocumentPropertiesW</c>.</summary>
    /// <param name="window">The parent window, or zero.</param>
    /// <param name="printer">The printer handle.</param>
    /// <param name="name">The printer name.</param>
    /// <param name="output">Receives the settings, or null.</param>
    /// <param name="input">Settings to merge, or null.</param>
    /// <param name="mode">What to do; zero asks for the size.</param>
    /// <returns>The size, or a status.</returns>
    [LibraryImport("winspool.drv", EntryPoint = "DocumentPropertiesW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial int DocumentProperties(nint window, nint printer, string name, byte* output, byte* input, uint mode);

    /// <summary>Native <c>CreateDCW</c>.</summary>
    /// <param name="driver">The driver, <c>WINSPOOL</c>.</param>
    /// <param name="device">The printer.</param>
    /// <param name="port">Must be null.</param>
    /// <param name="settings">The printer settings.</param>
    /// <returns>The device context, or zero.</returns>
    [LibraryImport("gdi32.dll", EntryPoint = "CreateDCW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint CreateDC(string driver, string device, char* port, byte* settings);

    /// <summary>Native <c>DeleteDC</c>.</summary>
    /// <param name="deviceContext">The device context.</param>
    /// <returns>Nonzero on success.</returns>
    [LibraryImport("gdi32.dll", EntryPoint = "DeleteDC")]
    internal static partial int DeleteDC(nint deviceContext);

    /// <summary>Native <c>GetDeviceCaps</c>.</summary>
    /// <param name="deviceContext">The device context.</param>
    /// <param name="index">The capability.</param>
    /// <returns>The value.</returns>
    [LibraryImport("gdi32.dll", EntryPoint = "GetDeviceCaps")]
    internal static partial int GetDeviceCaps(nint deviceContext, int index);

    /// <summary>Native <c>StartDocW</c>.</summary>
    /// <param name="deviceContext">The device context.</param>
    /// <param name="info">The job.</param>
    /// <returns>The job id, or zero or less on failure.</returns>
    [LibraryImport("gdi32.dll", EntryPoint = "StartDocW")]
    internal static partial int StartDoc(nint deviceContext, DocumentInfo* info);

    /// <summary>Native <c>StartPage</c>.</summary>
    /// <param name="deviceContext">The device context.</param>
    /// <returns>Greater than zero on success.</returns>
    [LibraryImport("gdi32.dll", EntryPoint = "StartPage")]
    internal static partial int StartPage(nint deviceContext);

    /// <summary>Native <c>EndPage</c>.</summary>
    /// <param name="deviceContext">The device context.</param>
    /// <returns>Greater than zero on success.</returns>
    [LibraryImport("gdi32.dll", EntryPoint = "EndPage")]
    internal static partial int EndPage(nint deviceContext);

    /// <summary>Native <c>EndDoc</c>.</summary>
    /// <param name="deviceContext">The device context.</param>
    /// <returns>Greater than zero on success.</returns>
    [LibraryImport("gdi32.dll", EntryPoint = "EndDoc")]
    internal static partial int EndDoc(nint deviceContext);

    /// <summary>Native <c>AbortDoc</c>.</summary>
    /// <param name="deviceContext">The device context.</param>
    /// <returns>Greater than zero on success.</returns>
    [LibraryImport("gdi32.dll", EntryPoint = "AbortDoc")]
    internal static partial int AbortDoc(nint deviceContext);

    /// <summary>Native <c>StretchDIBits</c>.</summary>
    /// <param name="deviceContext">The device context.</param>
    /// <param name="destinationX">The destination left.</param>
    /// <param name="destinationY">The destination top.</param>
    /// <param name="destinationWidth">The destination width.</param>
    /// <param name="destinationHeight">The destination height.</param>
    /// <param name="sourceX">The source left.</param>
    /// <param name="sourceY">The source top.</param>
    /// <param name="sourceWidth">The source width.</param>
    /// <param name="sourceHeight">The source height.</param>
    /// <param name="bits">The pixels.</param>
    /// <param name="info">The pixel format.</param>
    /// <param name="usage">Colour table use.</param>
    /// <param name="operation">The raster operation.</param>
    /// <returns>The scan lines copied, or zero on failure.</returns>
    [LibraryImport("gdi32.dll", EntryPoint = "StretchDIBits")]
    internal static partial int StretchDIBits(
        nint deviceContext,
        int destinationX,
        int destinationY,
        int destinationWidth,
        int destinationHeight,
        int sourceX,
        int sourceY,
        int sourceWidth,
        int sourceHeight,
        void* bits,
        BitmapInfoHeader* info,
        uint usage,
        uint operation);

    /// <summary>Native <c>PrintDlgExW</c>.</summary>
    /// <param name="dialog">The dialog settings and results.</param>
    /// <returns>An HRESULT.</returns>
    [LibraryImport("comdlg32.dll", EntryPoint = "PrintDlgExW")]
    internal static partial int PrintDlgEx(PrintDialogEx* dialog);

    /// <summary>Native <c>GlobalFree</c>.</summary>
    /// <param name="memory">The memory.</param>
    /// <returns>Zero on success.</returns>
    [LibraryImport("kernel32.dll", EntryPoint = "GlobalFree")]
    internal static partial nint GlobalFree(nint memory);

    /// <summary>Gets a COM pointer to an object's interface, for the native functions that take one. The caller must release the pointer.</summary>
    /// <typeparam name="T">The COM interface.</typeparam>
    /// <param name="provider">The object.</param>
    /// <returns>The pointer, counted for the caller.</returns>
    internal static nint ComPointerOf<T>(T provider)
        where T : class
    {
        var pointer = ComInterfaceMarshaller<T>.ConvertToUnmanaged(provider);
        return (nint)pointer;
    }

    /// <summary>Native <c>SetWindowSubclass</c>, which puts a procedure in front of a window's own, so it sees a message first.</summary>
    /// <param name="window">The window.</param>
    /// <param name="procedure">The procedure.</param>
    /// <param name="subclassId">A number that tells this subclass from others on the same procedure.</param>
    /// <param name="referenceData">A value passed back to the procedure.</param>
    /// <returns>Nonzero on success.</returns>
    [LibraryImport("comctl32.dll", EntryPoint = "SetWindowSubclass")]
    internal static partial int SetWindowSubclass(nint window, delegate* unmanaged<nint, uint, nuint, nint, nuint, nuint, nint> procedure, nuint subclassId, nuint referenceData);

    /// <summary>Native <c>RemoveWindowSubclass</c>.</summary>
    /// <param name="window">The window.</param>
    /// <param name="procedure">The procedure.</param>
    /// <param name="subclassId">The number it was added with.</param>
    /// <returns>Nonzero on success.</returns>
    [LibraryImport("comctl32.dll", EntryPoint = "RemoveWindowSubclass")]
    internal static partial int RemoveWindowSubclass(nint window, delegate* unmanaged<nint, uint, nuint, nint, nuint, nuint, nint> procedure, nuint subclassId);

    /// <summary>Native <c>DefSubclassProc</c>, which passes a message on to the next procedure.</summary>
    /// <param name="window">The window.</param>
    /// <param name="message">The message.</param>
    /// <param name="wordParameter">The first parameter.</param>
    /// <param name="longParameter">The second parameter.</param>
    /// <returns>The result of the window's own procedure.</returns>
    [LibraryImport("comctl32.dll", EntryPoint = "DefSubclassProc")]
    internal static partial nint DefSubclassProc(nint window, uint message, nuint wordParameter, nint longParameter);

    /// <summary>Native <c>UiaReturnRawElementProvider</c>, which answers a <c>WM_GETOBJECT</c> with a provider.</summary>
    /// <param name="window">The window.</param>
    /// <param name="wordParameter">The message's first parameter.</param>
    /// <param name="longParameter">The message's second parameter.</param>
    /// <param name="provider">The provider's <c>IRawElementProviderSimple</c>, or null to disconnect the window's providers.</param>
    /// <returns>The result to return from the message.</returns>
    [LibraryImport("uiautomationcore.dll", EntryPoint = "UiaReturnRawElementProvider")]
    internal static partial nint UiaReturnRawElementProvider(nint window, nuint wordParameter, nint longParameter, nint provider);

    /// <summary>Native <c>UiaHostProviderFromHwnd</c>.</summary>
    /// <param name="window">The window.</param>
    /// <param name="provider">Receives the provider UI Automation makes for the window.</param>
    /// <returns>An HRESULT.</returns>
    [LibraryImport("uiautomationcore.dll", EntryPoint = "UiaHostProviderFromHwnd")]
    internal static partial int UiaHostProviderFromHwnd(nint window, out nint provider);

    /// <summary>Native <c>UiaClientsAreListening</c>.</summary>
    /// <returns>Nonzero when a client such as a screen reader has asked for events.</returns>
    [LibraryImport("uiautomationcore.dll", EntryPoint = "UiaClientsAreListening")]
    internal static partial int UiaClientsAreListening();

    /// <summary>Native <c>UiaRaiseAutomationEvent</c>.</summary>
    /// <param name="provider">The element's <c>IRawElementProviderSimple</c>.</param>
    /// <param name="eventId">The event.</param>
    /// <returns>An HRESULT.</returns>
    [LibraryImport("uiautomationcore.dll", EntryPoint = "UiaRaiseAutomationEvent")]
    internal static partial int UiaRaiseAutomationEvent(nint provider, int eventId);

    /// <summary>Native <c>UiaRaiseAutomationPropertyChangedEvent</c>.</summary>
    /// <param name="provider">The element's <c>IRawElementProviderSimple</c>.</param>
    /// <param name="propertyId">The property.</param>
    /// <param name="oldValue">The value before.</param>
    /// <param name="newValue">The value after.</param>
    /// <returns>An HRESULT.</returns>
    [LibraryImport("uiautomationcore.dll", EntryPoint = "UiaRaiseAutomationPropertyChangedEvent")]
    internal static partial int UiaRaiseAutomationPropertyChangedEvent(nint provider, int propertyId, Accessibility.Uia.ComVariant oldValue, Accessibility.Uia.ComVariant newValue);

    /// <summary>Native <c>UiaRaiseStructureChangedEvent</c>.</summary>
    /// <param name="provider">The element whose children changed.</param>
    /// <param name="changeType">How they changed.</param>
    /// <param name="runtimeId">A pointer to the runtime identifier of the changed child, or null.</param>
    /// <param name="runtimeIdLength">The length of the identifier.</param>
    /// <returns>An HRESULT.</returns>
    [LibraryImport("uiautomationcore.dll", EntryPoint = "UiaRaiseStructureChangedEvent")]
    internal static partial int UiaRaiseStructureChangedEvent(nint provider, int changeType, nint runtimeId, int runtimeIdLength);

    /// <summary>Native <c>UiaGetReservedNotSupportedValue</c>, the value a provider returns for an attribute it does not support.</summary>
    /// <param name="value">Receives the object.</param>
    /// <returns>An HRESULT.</returns>
    [LibraryImport("uiautomationcore.dll", EntryPoint = "UiaGetReservedNotSupportedValue")]
    internal static partial int UiaGetReservedNotSupportedValue(out nint value);

    /// <summary>Native <c>UiaDisconnectProvider</c>.</summary>
    /// <param name="provider">The provider.</param>
    /// <returns>An HRESULT.</returns>
    [LibraryImport("uiautomationcore.dll", EntryPoint = "UiaDisconnectProvider")]
    internal static partial int UiaDisconnectProvider(nint provider);

    /// <summary>Native <c>SafeArrayCreateVector</c>.</summary>
    /// <param name="type">The element type, such as <c>VT_I4</c>.</param>
    /// <param name="lowerBound">The first index.</param>
    /// <param name="count">The number of elements.</param>
    /// <returns>The <c>SAFEARRAY</c>, or null.</returns>
    [LibraryImport("oleaut32.dll", EntryPoint = "SafeArrayCreateVector")]
    internal static partial nint SafeArrayCreateVector(ushort type, int lowerBound, uint count);

    /// <summary>Native <c>SafeArrayAccessData</c>.</summary>
    /// <param name="array">The array.</param>
    /// <param name="data">Receives a pointer to the elements.</param>
    /// <returns>An HRESULT.</returns>
    [LibraryImport("oleaut32.dll", EntryPoint = "SafeArrayAccessData")]
    internal static partial int SafeArrayAccessData(nint array, out void* data);

    /// <summary>Native <c>SafeArrayUnaccessData</c>.</summary>
    /// <param name="array">The array.</param>
    /// <returns>An HRESULT.</returns>
    [LibraryImport("oleaut32.dll", EntryPoint = "SafeArrayUnaccessData")]
    internal static partial int SafeArrayUnaccessData(nint array);

    /// <summary>Native <c>SafeArrayPutElement</c>.</summary>
    /// <param name="array">The array.</param>
    /// <param name="index">The index of the element.</param>
    /// <param name="element">The element, which for an object array is the object's pointer.</param>
    /// <returns>An HRESULT.</returns>
    [LibraryImport("oleaut32.dll", EntryPoint = "SafeArrayPutElement")]
    internal static partial int SafeArrayPutElement(nint array, int* index, void* element);
}
