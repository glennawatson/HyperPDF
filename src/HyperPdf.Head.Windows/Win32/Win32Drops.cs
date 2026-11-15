// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Head.Windows.Win32;

/// <summary>Reads the files out of a drop, which Windows hands over as a handle.</summary>
internal static unsafe class Win32Drops
{
    /// <summary>The index that asks for the number of files instead of one of them.</summary>
    private const uint Count = 0xFFFFFFFF;

    /// <summary>Reads every path of a drop and lets the drop go.</summary>
    /// <param name="drop">The handle from the drop message.</param>
    /// <returns>The paths of the files dropped.</returns>
    internal static string[] Read(nint drop)
    {
        var files = NativeMethods.DragQueryFile(drop, Count, null, 0);
        var paths = new string[files];
        for (uint i = 0; i < files; i++)
        {
            // The length does not count the terminator.
            var length = (int)NativeMethods.DragQueryFile(drop, i, null, 0);
            var buffer = new char[length + 1];
            fixed (char* name = buffer)
            {
                _ = NativeMethods.DragQueryFile(drop, i, name, (uint)buffer.Length);
            }

            paths[i] = new(buffer, 0, length);
        }

        NativeMethods.DragFinish(drop);
        return paths;
    }
}
