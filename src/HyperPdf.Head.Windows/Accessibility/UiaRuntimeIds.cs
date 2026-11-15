// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Head.Windows.Accessibility;

/// <summary>Makes the SAFEARRAY runtime id UI Automation uses to tell elements of a window apart.</summary>
internal static unsafe class UiaRuntimeIds
{
    /// <summary>The VT_I4 element type.</summary>
    private const ushort Int32Type = 3;

    /// <summary>The number of ints in the runtime id.</summary>
    private const uint Length = 2;

    /// <summary>Makes a runtime id for a node: the prefix that asks UI Automation to add the window's own id, then the node id.</summary>
    /// <param name="nodeId">The node id.</param>
    /// <returns>The SAFEARRAY, or zero when it could not be made.</returns>
    internal static nint Create(int nodeId)
    {
        var array = NativeMethods.CreateVector(Int32Type, 0, Length);
        if (array == 0)
        {
            return 0;
        }

        int* ids = stackalloc int[(int)Length];
        ids[0] = UiaIds.AppendRuntimeId;
        ids[1] = nodeId;
        for (var index = 0; index < (int)Length; index++)
        {
            _ = NativeMethods.PutElement(array, &index, &ids[index]);
        }

        return array;
    }
}
