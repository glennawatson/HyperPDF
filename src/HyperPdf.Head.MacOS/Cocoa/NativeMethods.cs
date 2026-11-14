// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Head.MacOS.Cocoa;

/// <summary>
/// Source generated entry points of the Objective-C runtime, AppKit, Core Graphics and Core Foundation. Every Cocoa call is a
/// message, so <c>objc_msgSend</c> is imported once for each argument and result shape the head sends.
/// </summary>
internal static unsafe partial class NativeMethods
{
    /// <summary>The AppKit framework, which holds the window classes.</summary>
    internal const string AppKit = "/System/Library/Frameworks/AppKit.framework/AppKit";

    /// <summary>The QuartzCore framework, which holds the layer classes.</summary>
    internal const string QuartzCore = "/System/Library/Frameworks/QuartzCore.framework/QuartzCore";

    /// <summary>The Objective-C runtime library.</summary>
    private const string ObjectiveC = "/usr/lib/libobjc.A.dylib";

    /// <summary>The Core Graphics framework.</summary>
    private const string CoreGraphics = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";

    /// <summary>The Core Foundation framework.</summary>
    private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    /// <summary>Native <c>objc_getClass</c>.</summary>
    /// <param name="name">The class name.</param>
    /// <returns>The class.</returns>
    [LibraryImport(ObjectiveC, EntryPoint = "objc_getClass", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial nint GetClass(string name);

    /// <summary>Native <c>sel_registerName</c>.</summary>
    /// <param name="name">The selector name.</param>
    /// <returns>The selector.</returns>
    [LibraryImport(ObjectiveC, EntryPoint = "sel_registerName", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial nint Selector(string name);

    /// <summary>Native <c>objc_autoreleasePoolPush</c>.</summary>
    /// <returns>The pool token.</returns>
    [LibraryImport(ObjectiveC, EntryPoint = "objc_autoreleasePoolPush")]
    internal static partial nint AutoreleasePoolPush();

    /// <summary>Native <c>objc_autoreleasePoolPop</c>.</summary>
    /// <param name="pool">The pool token.</param>
    [LibraryImport(ObjectiveC, EntryPoint = "objc_autoreleasePoolPop")]
    internal static partial void AutoreleasePoolPop(nint pool);

    /// <summary>Sends a message with no arguments that returns an object or an integer the size of a pointer.</summary>
    /// <param name="receiver">The receiver.</param>
    /// <param name="selector">The selector.</param>
    /// <returns>The result.</returns>
    [LibraryImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    internal static partial nint Send(nint receiver, nint selector);

    /// <summary>Sends a message with one object argument that returns an object.</summary>
    /// <param name="receiver">The receiver.</param>
    /// <param name="selector">The selector.</param>
    /// <param name="argument">The argument.</param>
    /// <returns>The result.</returns>
    [LibraryImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    internal static partial nint Send(nint receiver, nint selector, nint argument);

    /// <summary>Sends a message with no arguments that returns an unsigned integer, such as an event type or its modifier flags.</summary>
    /// <param name="receiver">The receiver.</param>
    /// <param name="selector">The selector.</param>
    /// <returns>The result.</returns>
    [LibraryImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    internal static partial nuint SendCount(nint receiver, nint selector);

    /// <summary>Sends <c>keyCode</c>.</summary>
    /// <param name="receiver">The event.</param>
    /// <param name="selector">The selector.</param>
    /// <returns>The virtual key code.</returns>
    [LibraryImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    internal static partial ushort SendKeyCode(nint receiver, nint selector);

    /// <summary>Sends a message with no arguments that returns yes or no.</summary>
    /// <param name="receiver">The receiver.</param>
    /// <param name="selector">The selector.</param>
    /// <returns>Nonzero for yes.</returns>
    [LibraryImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    internal static partial byte SendFlag(nint receiver, nint selector);

    /// <summary>Sends a message with no arguments that returns a floating point number.</summary>
    /// <param name="receiver">The receiver.</param>
    /// <param name="selector">The selector.</param>
    /// <returns>The result.</returns>
    [LibraryImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    internal static partial double SendDouble(nint receiver, nint selector);

    /// <summary>Sends a message with no arguments that returns a point, which both processor families return in registers.</summary>
    /// <param name="receiver">The receiver.</param>
    /// <param name="selector">The selector.</param>
    /// <returns>The point.</returns>
    [LibraryImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    internal static partial CocoaPoint SendPoint(nint receiver, nint selector);

    /// <summary>Sends a message with no arguments that returns a rectangle, on Apple silicon, where it comes back in registers.</summary>
    /// <param name="receiver">The receiver.</param>
    /// <param name="selector">The selector.</param>
    /// <returns>The rectangle.</returns>
    [LibraryImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    internal static partial CocoaRect SendRect(nint receiver, nint selector);

    /// <summary>Sends a message with no arguments that returns a rectangle, on Intel, where the caller passes the memory for it.</summary>
    /// <param name="result">Receives the rectangle.</param>
    /// <param name="receiver">The receiver.</param>
    /// <param name="selector">The selector.</param>
    [LibraryImport(ObjectiveC, EntryPoint = "objc_msgSend_stret")]
    internal static partial void SendRectIntel(CocoaRect* result, nint receiver, nint selector);

    /// <summary>Sends a message that takes a rectangle and returns one, on Apple silicon.</summary>
    /// <param name="receiver">The receiver.</param>
    /// <param name="selector">The selector.</param>
    /// <param name="argument">The rectangle argument.</param>
    /// <returns>The rectangle.</returns>
    [LibraryImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    internal static partial CocoaRect SendRectFromRect(nint receiver, nint selector, CocoaRect argument);

    /// <summary>Sends a message that takes a rectangle and returns one, on Intel.</summary>
    /// <param name="result">Receives the rectangle.</param>
    /// <param name="receiver">The receiver.</param>
    /// <param name="selector">The selector.</param>
    /// <param name="argument">The rectangle argument.</param>
    [LibraryImport(ObjectiveC, EntryPoint = "objc_msgSend_stret")]
    internal static partial void SendRectFromRectIntel(CocoaRect* result, nint receiver, nint selector, CocoaRect argument);

    /// <summary>Sends a message with no arguments and no result.</summary>
    /// <param name="receiver">The receiver.</param>
    /// <param name="selector">The selector.</param>
    [LibraryImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    internal static partial void SendVoid(nint receiver, nint selector);

    /// <summary>Sends a message with one object argument and no result.</summary>
    /// <param name="receiver">The receiver.</param>
    /// <param name="selector">The selector.</param>
    /// <param name="argument">The argument.</param>
    [LibraryImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    internal static partial void SendVoid(nint receiver, nint selector, nint argument);

    /// <summary>Sends a message with one boolean argument and no result.</summary>
    /// <param name="receiver">The receiver.</param>
    /// <param name="selector">The selector.</param>
    /// <param name="argument">The argument, 1 for yes.</param>
    [LibraryImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    internal static partial void SendVoidFlag(nint receiver, nint selector, byte argument);

    /// <summary>Sends a message with one floating point argument and no result, such as <c>setContentsScale:</c>.</summary>
    /// <param name="receiver">The receiver.</param>
    /// <param name="selector">The selector.</param>
    /// <param name="argument">The argument.</param>
    [LibraryImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    internal static partial void SendVoidDouble(nint receiver, nint selector, double argument);

    /// <summary>Sends <c>initWithContentRect:styleMask:backing:defer:</c>.</summary>
    /// <param name="receiver">The allocated window.</param>
    /// <param name="selector">The selector.</param>
    /// <param name="content">The content rectangle in points.</param>
    /// <param name="style">The style mask.</param>
    /// <param name="backing">The backing store type.</param>
    /// <param name="defer">Nonzero to make the window device later.</param>
    /// <returns>The window.</returns>
    [LibraryImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    internal static partial nint SendInitWindow(nint receiver, nint selector, CocoaRect content, nuint style, nuint backing, byte defer);

    /// <summary>Sends <c>nextEventMatchingMask:untilDate:inMode:dequeue:</c>.</summary>
    /// <param name="receiver">The application.</param>
    /// <param name="selector">The selector.</param>
    /// <param name="mask">The event types wanted.</param>
    /// <param name="until">The date to wait until.</param>
    /// <param name="mode">The run loop mode.</param>
    /// <param name="dequeue">Nonzero to take the event off the queue.</param>
    /// <returns>The event, or 0 when none arrived in time.</returns>
    [LibraryImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    internal static partial nint SendNextEvent(nint receiver, nint selector, nuint mask, nint until, nint mode, byte dequeue);

    /// <summary>Sends <c>dateWithTimeIntervalSinceNow:</c>.</summary>
    /// <param name="receiver">The NSDate class.</param>
    /// <param name="selector">The selector.</param>
    /// <param name="seconds">The seconds from now.</param>
    /// <returns>The autoreleased date.</returns>
    [LibraryImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    internal static partial nint SendDateSinceNow(nint receiver, nint selector, double seconds);

    /// <summary>Sends <c>otherEventWithType:location:modifierFlags:timestamp:windowNumber:context:subtype:data1:data2:</c>.</summary>
    /// <param name="receiver">The NSEvent class.</param>
    /// <param name="selector">The selector.</param>
    /// <param name="type">The event type.</param>
    /// <param name="location">The location.</param>
    /// <param name="flags">The modifier flags.</param>
    /// <param name="timestamp">The time of the event.</param>
    /// <param name="windowNumber">The window number.</param>
    /// <param name="context">The graphics context, or 0.</param>
    /// <param name="subtype">The subtype.</param>
    /// <param name="data1">The first data value.</param>
    /// <param name="data2">The second data value.</param>
    /// <returns>The autoreleased event.</returns>
    [LibraryImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    internal static partial nint SendOtherEvent(
        nint receiver,
        nint selector,
        nuint type,
        CocoaPoint location,
        nuint flags,
        double timestamp,
        nint windowNumber,
        nint context,
        short subtype,
        nint data1,
        nint data2);

    /// <summary>Sends <c>postEvent:atStart:</c>.</summary>
    /// <param name="receiver">The application.</param>
    /// <param name="selector">The selector.</param>
    /// <param name="posted">The event.</param>
    /// <param name="atStart">Nonzero to put the event at the front of the queue.</param>
    [LibraryImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    internal static partial void SendPostEvent(nint receiver, nint selector, nint posted, byte atStart);

    /// <summary>Sends <c>stringWithUTF8String:</c>.</summary>
    /// <param name="receiver">The NSString class.</param>
    /// <param name="selector">The selector.</param>
    /// <param name="text">The null terminated UTF-8 text.</param>
    /// <returns>The autoreleased string.</returns>
    [LibraryImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    internal static partial nint SendText(nint receiver, nint selector, byte* text);

    /// <summary>Sends <c>accessibilityElementWithRole:frame:label:parent:</c>.</summary>
    /// <param name="receiver">The NSAccessibilityElement class.</param>
    /// <param name="selector">The selector.</param>
    /// <param name="role">The role as an NSString.</param>
    /// <param name="frame">The frame on the screen in points.</param>
    /// <param name="label">The label as an NSString.</param>
    /// <param name="parent">The parent element.</param>
    /// <returns>The autoreleased element.</returns>
    [LibraryImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    internal static partial nint SendElement(nint receiver, nint selector, nint role, CocoaRect frame, nint label, nint parent);

    /// <summary>Native <c>NSAccessibilityPostNotification</c>.</summary>
    /// <param name="element">The element that changed.</param>
    /// <param name="notification">The notification name as an NSString.</param>
    [LibraryImport(AppKit, EntryPoint = "NSAccessibilityPostNotification")]
    internal static partial void AccessibilityPostNotification(nint element, nint notification);

    /// <summary>Native <c>CFDataCreate</c>.</summary>
    /// <param name="allocator">The allocator, or 0 for the default.</param>
    /// <param name="bytes">The bytes to copy.</param>
    /// <param name="length">The number of bytes.</param>
    /// <returns>The data object.</returns>
    [LibraryImport(CoreFoundation, EntryPoint = "CFDataCreate")]
    internal static partial nint DataCreate(nint allocator, byte* bytes, nint length);

    /// <summary>Native <c>CFRelease</c>.</summary>
    /// <param name="value">The object.</param>
    [LibraryImport(CoreFoundation, EntryPoint = "CFRelease")]
    internal static partial void Release(nint value);

    /// <summary>Native <c>CGDataProviderCreateWithCFData</c>.</summary>
    /// <param name="data">The data object.</param>
    /// <returns>The data provider.</returns>
    [LibraryImport(CoreGraphics, EntryPoint = "CGDataProviderCreateWithCFData")]
    internal static partial nint DataProviderCreate(nint data);

    /// <summary>Native <c>CGColorSpaceCreateDeviceRGB</c>.</summary>
    /// <returns>The colour space.</returns>
    [LibraryImport(CoreGraphics, EntryPoint = "CGColorSpaceCreateDeviceRGB")]
    internal static partial nint ColorSpaceCreateDeviceRgb();

    /// <summary>Native <c>CGImageCreate</c>.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="bitsPerComponent">The bits per channel.</param>
    /// <param name="bitsPerPixel">The bits per pixel.</param>
    /// <param name="bytesPerRow">The bytes per row.</param>
    /// <param name="colorSpace">The colour space.</param>
    /// <param name="bitmapInfo">The channel order and alpha.</param>
    /// <param name="provider">The pixels.</param>
    /// <param name="decode">The decode array, or null.</param>
    /// <param name="shouldInterpolate">Nonzero to smooth when scaled.</param>
    /// <param name="intent">The rendering intent.</param>
    /// <returns>The image.</returns>
    [LibraryImport(CoreGraphics, EntryPoint = "CGImageCreate")]
    internal static partial nint ImageCreate(
        nuint width,
        nuint height,
        nuint bitsPerComponent,
        nuint bitsPerPixel,
        nuint bytesPerRow,
        nint colorSpace,
        uint bitmapInfo,
        nint provider,
        double* decode,
        byte shouldInterpolate,
        int intent);
}
