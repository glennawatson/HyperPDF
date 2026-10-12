// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
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
}
