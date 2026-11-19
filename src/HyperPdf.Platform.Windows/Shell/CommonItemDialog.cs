// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace HyperPdf.Platform.Windows.Shell;

/// <summary>Runs the Windows Shell file and folder dialogs on the owning window thread.</summary>
[SupportedOSPlatform("windows")]
public static unsafe class CommonItemDialog
{
    /// <summary>Opens a modal file or folder chooser.</summary>
    /// <param name="owner">The owner window.</param>
    /// <param name="folder">The initial directory.</param>
    /// <param name="saveName">The suggested name for a save, or null for an open dialog.</param>
    /// <param name="multiple">Whether to allow several files.</param>
    /// <param name="directories">Whether to choose folders.</param>
    /// <param name="title">The dialog title.</param>
    /// <returns>The chosen paths, empty on cancellation.</returns>
    public static string[] Choose(nint owner, string folder, string? saveName, bool multiple, bool directories, string title)
    {
        const uint apartmentThreaded = 2;
        const int cancelled = unchecked((int)0x800704C7);
        var cls = saveName is null ? new Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7") : new Guid("C0B4E2F3-BA21-4773-8DBA-335EC946EB8B");
        var iid = saveName is null ? new Guid("D57C7288-D4AD-4768-BE02-9D969532D960") : new Guid("84BCCD23-5FDE-4CDB-AEA4-AF64B83D78AB");
        Marshal.ThrowExceptionForHR(NativeMethods.CoInitializeEx(0, apartmentThreaded));
        try
        {
            nint dialog;
            Marshal.ThrowExceptionForHR(NativeMethods.CoCreateInstance(&cls, null, 1, &iid, (void**)&dialog));
            try
            {
                Configure(dialog, folder, saveName, multiple, directories, title);
                const int show = 3;
                var result = ((delegate* unmanaged[Stdcall]<nint, nint, int>)Slot(dialog, show))(dialog, owner);
                if (result == cancelled)
                {
                    return [];
                }

                Marshal.ThrowExceptionForHR(result);
                const int getResult = 20;
                return multiple ? ReadMany(dialog) : [ReadPath(GetObject(dialog, getResult))];
            }
            finally
            {
                _ = Marshal.Release(dialog);
            }
        }
        finally
        {
            NativeMethods.CoUninitialize();
        }
    }

    /// <summary>Configures filesystem selection while keeping the Shell's validation and overwrite prompts.</summary>
    /// <param name="dialog">The dialog interface.</param>
    /// <param name="folder">The initial directory.</param>
    /// <param name="saveName">The suggested name.</param>
    /// <param name="multiple">Whether to allow several files.</param>
    /// <param name="directories">Whether to select folders.</param>
    /// <param name="title">The dialog title.</param>
    private static void Configure(nint dialog, string folder, string? saveName, bool multiple, bool directories, string title)
    {
        const int getOptions = 10;
        const int setOptions = 9;
        const int setFolder = 12;
        const int setName = 15;
        const int setTitle = 17;
        const uint filesystem = 0x40;
        const uint multiSelect = 0x200;
        const uint pickFolders = 0x20;
        uint options;
        Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, uint*, int>)Slot(dialog, getOptions))(dialog, &options));
        options |= filesystem | (multiple ? multiSelect : 0) | (directories ? pickFolders : 0);
        Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, uint, int>)Slot(dialog, setOptions))(dialog, options));
        fixed (char* caption = title)
        {
            Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, char*, int>)Slot(dialog, setTitle))(dialog, caption));
        }

        if (saveName is not null)
        {
            fixed (char* name = saveName)
            {
                Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, char*, int>)Slot(dialog, setName))(dialog, name));
            }
        }

        var iid = new Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE");
        nint directory;
        if (NativeMethods.CreateShellItem(folder, 0, &iid, &directory) >= 0)
        {
            try
            {
                Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, nint, int>)Slot(dialog, setFolder))(dialog, directory));
            }
            finally
            {
                _ = Marshal.Release(directory);
            }
        }
    }

    /// <summary>Reads the Shell item array from a multiple selection.</summary>
    /// <param name="dialog">The open dialog.</param>
    /// <returns>The chosen paths.</returns>
    private static string[] ReadMany(nint dialog)
    {
        const int getResults = 27;
        const int getCount = 7;
        const int getItem = 8;
        var array = GetObject(dialog, getResults);
        try
        {
            uint count;
            Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, uint*, int>)Slot(array, getCount))(array, &count));
            var paths = new string[count];
            for (uint i = 0; i < count; i++)
            {
                nint item;
                Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, uint, nint*, int>)Slot(array, getItem))(array, i, &item));
                paths[i] = ReadPath(item);
            }

            return paths;
        }
        finally
        {
            _ = Marshal.Release(array);
        }
    }

    /// <summary>Gets a result interface.</summary>
    /// <param name="dialog">The dialog.</param>
    /// <param name="method">The result method slot.</param>
    /// <returns>The owned interface.</returns>
    private static nint GetObject(nint dialog, int method)
    {
        nint result;
        Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, nint*, int>)Slot(dialog, method))(dialog, &result));
        return result;
    }

    /// <summary>Reads a filesystem path and releases the selected Shell item.</summary>
    /// <param name="item">The owned item.</param>
    /// <returns>The path.</returns>
    /// <exception cref="IOException">The Shell returned no path.</exception>
    private static string ReadPath(nint item)
    {
        const int getDisplayName = 5;
        const uint filesystemPath = 0x80058000;
        nint text = 0;
        try
        {
            Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, uint, nint*, int>)Slot(item, getDisplayName))(item, filesystemPath, &text));
            return Marshal.PtrToStringUni(text) ?? throw new IOException("The Shell returned no path.");
        }
        finally
        {
            Marshal.FreeCoTaskMem(text);
            _ = Marshal.Release(item);
        }
    }

    /// <summary>Gets the address of a COM interface method.</summary>
    /// <param name="instance">The interface.</param>
    /// <param name="index">The method slot.</param>
    /// <returns>The method address.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nint Slot(nint instance, int index) => (*(nint**)instance)[index];
}
