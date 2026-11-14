// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using System.Text;

namespace HyperPdf.Head.MacOS.Cocoa;

/// <summary>Small Cocoa helpers the window and the accessibility bridge share: strings and rectangles that come back differently on Intel and Apple silicon.</summary>
internal static unsafe class Cocoa
{
    /// <summary>The longest text turned into an NSString on the stack.</summary>
    private const int StackTextBytes = 512;

    /// <summary>Makes an autoreleased NSString from text.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The string.</returns>
    internal static nint Text(string text)
    {
        var length = Encoding.UTF8.GetMaxByteCount(text.Length) + 1;
        var bytes = length <= StackTextBytes ? stackalloc byte[StackTextBytes] : new byte[length];
        var written = Encoding.UTF8.GetBytes(text, bytes);
        bytes[written] = 0;
        fixed (byte* pointer = bytes)
        {
            return NativeMethods.SendText(NativeMethods.GetClass("NSString"), Selectors.StringWithUtf8, pointer);
        }
    }

    /// <summary>Asks an object for a rectangle.</summary>
    /// <param name="receiver">The object.</param>
    /// <param name="selector">The selector of a method that returns a rectangle.</param>
    /// <returns>The rectangle.</returns>
    internal static CocoaRect Rect(nint receiver, nint selector)
    {
        if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
        {
            return NativeMethods.SendRect(receiver, selector);
        }

        // Intel returns a structure this large through memory the caller passes, with a different entry point.
        CocoaRect rect;
        NativeMethods.SendRectIntel(&rect, receiver, selector);
        return rect;
    }

    /// <summary>Asks an object to turn one rectangle into another.</summary>
    /// <param name="receiver">The object.</param>
    /// <param name="selector">The selector of a method that takes and returns a rectangle.</param>
    /// <param name="argument">The rectangle argument.</param>
    /// <returns>The rectangle.</returns>
    internal static CocoaRect Rect(nint receiver, nint selector, in CocoaRect argument)
    {
        if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
        {
            return NativeMethods.SendRectFromRect(receiver, selector, argument);
        }

        CocoaRect rect;
        NativeMethods.SendRectFromRectIntel(&rect, receiver, selector, argument);
        return rect;
    }
}
