// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Head.MacOS.Cocoa;

/// <summary>The Objective-C selectors the head sends, looked up once. A selector never changes for the life of the process.</summary>
internal static class Selectors
{
    /// <summary>Gets <c>alloc</c>.</summary>
    internal static nint Alloc { get; } = NativeMethods.Selector("alloc");

    /// <summary>Gets <c>release</c>.</summary>
    internal static nint Release { get; } = NativeMethods.Selector("release");

    /// <summary>Gets <c>sharedApplication</c>.</summary>
    internal static nint SharedApplication { get; } = NativeMethods.Selector("sharedApplication");

    /// <summary>Gets <c>setActivationPolicy:</c>.</summary>
    internal static nint SetActivationPolicy { get; } = NativeMethods.Selector("setActivationPolicy:");

    /// <summary>Gets <c>finishLaunching</c>.</summary>
    internal static nint FinishLaunching { get; } = NativeMethods.Selector("finishLaunching");

    /// <summary>Gets <c>activateIgnoringOtherApps:</c>.</summary>
    internal static nint Activate { get; } = NativeMethods.Selector("activateIgnoringOtherApps:");

    /// <summary>Gets <c>initWithContentRect:styleMask:backing:defer:</c>.</summary>
    internal static nint InitWindow { get; } = NativeMethods.Selector("initWithContentRect:styleMask:backing:defer:");

    /// <summary>Gets <c>setReleasedWhenClosed:</c>.</summary>
    internal static nint SetReleasedWhenClosed { get; } = NativeMethods.Selector("setReleasedWhenClosed:");

    /// <summary>Gets <c>setAcceptsMouseMovedEvents:</c>.</summary>
    internal static nint SetAcceptsMouseMoved { get; } = NativeMethods.Selector("setAcceptsMouseMovedEvents:");

    /// <summary>Gets <c>center</c>.</summary>
    internal static nint Center { get; } = NativeMethods.Selector("center");

    /// <summary>Gets <c>contentView</c>.</summary>
    internal static nint ContentView { get; } = NativeMethods.Selector("contentView");

    /// <summary>Gets <c>setWantsLayer:</c>.</summary>
    internal static nint SetWantsLayer { get; } = NativeMethods.Selector("setWantsLayer:");

    /// <summary>Gets <c>layer</c>, which on a layer class makes a new layer.</summary>
    internal static nint Layer { get; } = NativeMethods.Selector("layer");

    /// <summary>Gets <c>setLayer:</c>.</summary>
    internal static nint SetLayer { get; } = NativeMethods.Selector("setLayer:");

    /// <summary>Gets <c>setContents:</c>.</summary>
    internal static nint SetContents { get; } = NativeMethods.Selector("setContents:");

    /// <summary>Gets <c>setContentsScale:</c>.</summary>
    internal static nint SetContentsScale { get; } = NativeMethods.Selector("setContentsScale:");

    /// <summary>Gets <c>bounds</c>.</summary>
    internal static nint Bounds { get; } = NativeMethods.Selector("bounds");

    /// <summary>Gets <c>frame</c>.</summary>
    internal static nint Frame { get; } = NativeMethods.Selector("frame");

    /// <summary>Gets <c>contentRectForFrameRect:</c>.</summary>
    internal static nint ContentRectForFrameRect { get; } = NativeMethods.Selector("contentRectForFrameRect:");

    /// <summary>Gets <c>screen</c>.</summary>
    internal static nint Screen { get; } = NativeMethods.Selector("screen");

    /// <summary>Gets <c>backingScaleFactor</c>.</summary>
    internal static nint BackingScaleFactor { get; } = NativeMethods.Selector("backingScaleFactor");

    /// <summary>Gets <c>windowNumber</c>.</summary>
    internal static nint WindowNumber { get; } = NativeMethods.Selector("windowNumber");

    /// <summary>Gets <c>isVisible</c>.</summary>
    internal static nint IsVisible { get; } = NativeMethods.Selector("isVisible");

    /// <summary>Gets <c>makeKeyAndOrderFront:</c>.</summary>
    internal static nint MakeKeyAndOrderFront { get; } = NativeMethods.Selector("makeKeyAndOrderFront:");

    /// <summary>Gets <c>close</c>.</summary>
    internal static nint Close { get; } = NativeMethods.Selector("close");

    /// <summary>Gets <c>setTitle:</c>.</summary>
    internal static nint SetTitle { get; } = NativeMethods.Selector("setTitle:");

    /// <summary>Gets <c>stringWithUTF8String:</c>.</summary>
    internal static nint StringWithUtf8 { get; } = NativeMethods.Selector("stringWithUTF8String:");

    /// <summary>Gets <c>nextEventMatchingMask:untilDate:inMode:dequeue:</c>.</summary>
    internal static nint NextEvent { get; } = NativeMethods.Selector("nextEventMatchingMask:untilDate:inMode:dequeue:");

    /// <summary>Gets <c>sendEvent:</c>.</summary>
    internal static nint SendEvent { get; } = NativeMethods.Selector("sendEvent:");

    /// <summary>Gets <c>postEvent:atStart:</c>.</summary>
    internal static nint PostEvent { get; } = NativeMethods.Selector("postEvent:atStart:");

    /// <summary>Gets <c>otherEventWithType:location:modifierFlags:timestamp:windowNumber:context:subtype:data1:data2:</c>.</summary>
    internal static nint OtherEvent { get; } = NativeMethods.Selector("otherEventWithType:location:modifierFlags:timestamp:windowNumber:context:subtype:data1:data2:");

    /// <summary>Gets <c>distantFuture</c>.</summary>
    internal static nint DistantFuture { get; } = NativeMethods.Selector("distantFuture");

    /// <summary>Gets <c>distantPast</c>.</summary>
    internal static nint DistantPast { get; } = NativeMethods.Selector("distantPast");

    /// <summary>Gets <c>dateWithTimeIntervalSinceNow:</c>.</summary>
    internal static nint DateSinceNow { get; } = NativeMethods.Selector("dateWithTimeIntervalSinceNow:");

    /// <summary>Gets <c>type</c>.</summary>
    internal static nint Type { get; } = NativeMethods.Selector("type");

    /// <summary>Gets <c>keyCode</c>.</summary>
    internal static nint KeyCode { get; } = NativeMethods.Selector("keyCode");

    /// <summary>Gets <c>characters</c>, the text a key event types.</summary>
    internal static nint Characters { get; } = NativeMethods.Selector("characters");

    /// <summary>Gets <c>UTF8String</c>.</summary>
    internal static nint Utf8String { get; } = NativeMethods.Selector("UTF8String");

    /// <summary>Gets <c>generalPasteboard</c>, the clipboard.</summary>
    internal static nint GeneralPasteboard { get; } = NativeMethods.Selector("generalPasteboard");

    /// <summary>Gets <c>clearContents</c>.</summary>
    internal static nint ClearContents { get; } = NativeMethods.Selector("clearContents");

    /// <summary>Gets <c>setString:forType:</c>.</summary>
    internal static nint SetStringForType { get; } = NativeMethods.Selector("setString:forType:");

    /// <summary>Gets <c>modifierFlags</c>.</summary>
    internal static nint ModifierFlags { get; } = NativeMethods.Selector("modifierFlags");

    /// <summary>Gets <c>locationInWindow</c>.</summary>
    internal static nint LocationInWindow { get; } = NativeMethods.Selector("locationInWindow");

    /// <summary>Gets <c>buttonNumber</c>.</summary>
    internal static nint ButtonNumber { get; } = NativeMethods.Selector("buttonNumber");

    /// <summary>Gets <c>scrollingDeltaX</c>.</summary>
    internal static nint ScrollingDeltaX { get; } = NativeMethods.Selector("scrollingDeltaX");

    /// <summary>Gets <c>scrollingDeltaY</c>.</summary>
    internal static nint ScrollingDeltaY { get; } = NativeMethods.Selector("scrollingDeltaY");

    /// <summary>Gets <c>hasPreciseScrollingDeltas</c>.</summary>
    internal static nint HasPreciseScrollingDeltas { get; } = NativeMethods.Selector("hasPreciseScrollingDeltas");

    /// <summary>Gets <c>set</c>, which makes a cursor the current one.</summary>
    internal static nint Set { get; } = NativeMethods.Selector("set");

    /// <summary>Gets <c>arrowCursor</c>.</summary>
    internal static nint ArrowCursor { get; } = NativeMethods.Selector("arrowCursor");

    /// <summary>Gets <c>pointingHandCursor</c>.</summary>
    internal static nint PointingHandCursor { get; } = NativeMethods.Selector("pointingHandCursor");

    /// <summary>Gets <c>crosshairCursor</c>.</summary>
    internal static nint CrosshairCursor { get; } = NativeMethods.Selector("crosshairCursor");

    /// <summary>Gets <c>IBeamCursor</c>.</summary>
    internal static nint BeamCursor { get; } = NativeMethods.Selector("IBeamCursor");

    /// <summary>Gets <c>openHandCursor</c>.</summary>
    internal static nint OpenHandCursor { get; } = NativeMethods.Selector("openHandCursor");

    /// <summary>Gets <c>sharedWorkspace</c>.</summary>
    internal static nint SharedWorkspace { get; } = NativeMethods.Selector("sharedWorkspace");

    /// <summary>Gets <c>isVoiceOverEnabled</c>.</summary>
    internal static nint IsVoiceOverEnabled { get; } = NativeMethods.Selector("isVoiceOverEnabled");

    /// <summary>Gets <c>setAccessibilityElement:</c>.</summary>
    internal static nint SetAccessibilityElement { get; } = NativeMethods.Selector("setAccessibilityElement:");

    /// <summary>Gets <c>setAccessibilityRole:</c>.</summary>
    internal static nint SetAccessibilityRole { get; } = NativeMethods.Selector("setAccessibilityRole:");

    /// <summary>Gets <c>setAccessibilityLabel:</c>.</summary>
    internal static nint SetAccessibilityLabel { get; } = NativeMethods.Selector("setAccessibilityLabel:");

    /// <summary>Gets <c>setAccessibilityValue:</c>.</summary>
    internal static nint SetAccessibilityValue { get; } = NativeMethods.Selector("setAccessibilityValue:");

    /// <summary>Gets <c>setAccessibilityHelp:</c>.</summary>
    internal static nint SetAccessibilityHelp { get; } = NativeMethods.Selector("setAccessibilityHelp:");

    /// <summary>Gets <c>setAccessibilityChildren:</c>.</summary>
    internal static nint SetAccessibilityChildren { get; } = NativeMethods.Selector("setAccessibilityChildren:");

    /// <summary>Gets <c>accessibilityElementWithRole:frame:label:parent:</c>.</summary>
    internal static nint AccessibilityElement { get; } = NativeMethods.Selector("accessibilityElementWithRole:frame:label:parent:");

    /// <summary>Gets <c>array</c>, which makes an empty mutable array.</summary>
    internal static nint Array { get; } = NativeMethods.Selector("array");

    /// <summary>Gets <c>addObject:</c>.</summary>
    internal static nint AddObject { get; } = NativeMethods.Selector("addObject:");
}
