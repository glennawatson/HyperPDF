// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Platform.Windows.Accessibility.Uia;

/// <summary>Makes the <c>SAFEARRAY</c>s UI Automation takes from providers. The receiver owns and frees an array it is given.</summary>
internal static unsafe class SafeArrays
{
    /// <summary>The element type of a 32 bit integer.</summary>
    private const ushort Int32Type = 3;

    /// <summary>The element type of a 64 bit float.</summary>
    private const ushort DoubleType = 5;

    /// <summary>The element type of an object.</summary>
    private const ushort UnknownType = 13;

    /// <summary>Makes an array of 32 bit integers.</summary>
    /// <param name="values">The values.</param>
    /// <returns>The array.</returns>
    internal static nint FromInt32(ReadOnlySpan<int> values)
    {
        var array = NativeMethods.SafeArrayCreateVector(Int32Type, 0, (uint)values.Length);
        Copy(array, values);
        return array;
    }

    /// <summary>Makes an array of 64 bit floats.</summary>
    /// <param name="values">The values.</param>
    /// <returns>The array.</returns>
    internal static nint FromDouble(ReadOnlySpan<double> values)
    {
        var array = NativeMethods.SafeArrayCreateVector(DoubleType, 0, (uint)values.Length);
        Copy(array, values);
        return array;
    }

    /// <summary>Makes an array of objects. The array counts each object itself, so the caller keeps its own count.</summary>
    /// <param name="objects">The <c>IUnknown</c> pointers.</param>
    /// <returns>The array.</returns>
    internal static nint FromUnknowns(ReadOnlySpan<nint> objects)
    {
        var array = NativeMethods.SafeArrayCreateVector(UnknownType, 0, (uint)objects.Length);
        for (var index = 0; index < objects.Length; index++)
        {
            Marshal.ThrowExceptionForHR(NativeMethods.SafeArrayPutElement(array, &index, (void*)objects[index]));
        }

        return array;
    }

    /// <summary>Copies values into an array's storage.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="array">The array.</param>
    /// <param name="values">The values.</param>
    private static void Copy<T>(nint array, ReadOnlySpan<T> values)
        where T : unmanaged
    {
        if (values.IsEmpty)
        {
            return;
        }

        Marshal.ThrowExceptionForHR(NativeMethods.SafeArrayAccessData(array, out var data));
        try
        {
            Span<T> target = new(data, values.Length);
            values.CopyTo(target);
        }
        finally
        {
            _ = NativeMethods.SafeArrayUnaccessData(array);
        }
    }
}
