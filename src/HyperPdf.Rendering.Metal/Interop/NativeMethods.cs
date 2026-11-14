// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Rendering.Metal.Interop;

/// <summary>
/// Source generated entry points of the Objective-C runtime and Metal. Every Metal call is a message, so <c>objc_msgSend</c> is
/// imported once for each argument shape the backend sends.
/// </summary>
internal static unsafe partial class NativeMethods
{
    /// <summary>The Objective-C runtime library.</summary>
    private const string ObjectiveC = "/usr/lib/libobjc.A.dylib";

    /// <summary>The Metal framework.</summary>
    private const string Metal = "/System/Library/Frameworks/Metal.framework/Metal";

    /// <summary>Native <c>MTLCreateSystemDefaultDevice</c>.</summary>
    /// <returns>The default GPU, or 0 when the machine has none Metal can use.</returns>
    [LibraryImport(Metal, EntryPoint = "MTLCreateSystemDefaultDevice")]
    internal static partial nint CreateSystemDefaultDevice();

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

    /// <summary>Sends a message with no arguments that returns an object or a pointer.</summary>
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

    /// <summary>Sends a message with no arguments that returns an unsigned integer, such as <c>storageMode</c>.</summary>
    /// <param name="receiver">The receiver.</param>
    /// <param name="selector">The selector.</param>
    /// <returns>The result.</returns>
    [LibraryImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    internal static partial nuint SendCount(nint receiver, nint selector);

    /// <summary>Sends a message with one unsigned integer argument that returns an object, such as <c>objectAtIndexedSubscript:</c>.</summary>
    /// <param name="receiver">The receiver.</param>
    /// <param name="selector">The selector.</param>
    /// <param name="argument">The argument.</param>
    /// <returns>The result.</returns>
    [LibraryImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    internal static partial nint SendIndex(nint receiver, nint selector, nuint argument);

    /// <summary>Sends a message with an object and an error pointer that returns an object, such as <c>newRenderPipelineStateWithDescriptor:error:</c>.</summary>
    /// <param name="receiver">The receiver.</param>
    /// <param name="selector">The selector.</param>
    /// <param name="argument">The object argument.</param>
    /// <param name="error">Receives an NSError, or 0.</param>
    /// <returns>The result.</returns>
    [LibraryImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    internal static partial nint SendWithError(nint receiver, nint selector, nint argument, nint* error);

    /// <summary>Sends <c>newLibraryWithSource:options:error:</c>.</summary>
    /// <param name="receiver">The device.</param>
    /// <param name="selector">The selector.</param>
    /// <param name="source">The shader source as an NSString.</param>
    /// <param name="options">The compile options, or 0.</param>
    /// <param name="error">Receives an NSError, or 0.</param>
    /// <returns>The library.</returns>
    [LibraryImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    internal static partial nint SendCompile(nint receiver, nint selector, nint source, nint options, nint* error);

    /// <summary>Sends <c>newBufferWithLength:options:</c>.</summary>
    /// <param name="receiver">The device.</param>
    /// <param name="selector">The selector.</param>
    /// <param name="length">The length in bytes.</param>
    /// <param name="options">The resource options.</param>
    /// <returns>The buffer.</returns>
    [LibraryImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    internal static partial nint SendNewBuffer(nint receiver, nint selector, nuint length, nuint options);

    /// <summary>Sends <c>texture2DDescriptorWithPixelFormat:width:height:mipmapped:</c>.</summary>
    /// <param name="receiver">The MTLTextureDescriptor class.</param>
    /// <param name="selector">The selector.</param>
    /// <param name="format">The pixel format.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="mipmapped">Nonzero for a mip chain.</param>
    /// <returns>The descriptor.</returns>
    [LibraryImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    internal static partial nint SendTextureDescriptor(nint receiver, nint selector, nuint format, nuint width, nuint height, byte mipmapped);

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

    /// <summary>Sends a message with one unsigned integer argument and no result.</summary>
    /// <param name="receiver">The receiver.</param>
    /// <param name="selector">The selector.</param>
    /// <param name="argument">The argument.</param>
    [LibraryImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    internal static partial void SendVoidIndex(nint receiver, nint selector, nuint argument);

    /// <summary>Sends a message with one boolean argument and no result.</summary>
    /// <param name="receiver">The receiver.</param>
    /// <param name="selector">The selector.</param>
    /// <param name="argument">The argument, 1 for yes.</param>
    [LibraryImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    internal static partial void SendVoidFlag(nint receiver, nint selector, byte argument);

    /// <summary>Sends a message with an object and an index and no result, such as <c>setFragmentTexture:atIndex:</c>.</summary>
    /// <param name="receiver">The receiver.</param>
    /// <param name="selector">The selector.</param>
    /// <param name="argument">The object.</param>
    /// <param name="index">The index.</param>
    [LibraryImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    internal static partial void SendVoidAtIndex(nint receiver, nint selector, nint argument, nuint index);

    /// <summary>Sends <c>setVertexBuffer:offset:atIndex:</c>.</summary>
    /// <param name="receiver">The encoder.</param>
    /// <param name="selector">The selector.</param>
    /// <param name="buffer">The buffer.</param>
    /// <param name="offset">The offset in bytes.</param>
    /// <param name="index">The buffer index.</param>
    [LibraryImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    internal static partial void SendVertexBuffer(nint receiver, nint selector, nint buffer, nuint offset, nuint index);

    /// <summary>Sends <c>setVertexBytes:length:atIndex:</c>.</summary>
    /// <param name="receiver">The encoder.</param>
    /// <param name="selector">The selector.</param>
    /// <param name="bytes">The bytes.</param>
    /// <param name="length">The length in bytes.</param>
    /// <param name="index">The buffer index.</param>
    [LibraryImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    internal static partial void SendVertexBytes(nint receiver, nint selector, void* bytes, nuint length, nuint index);

    /// <summary>Sends <c>drawPrimitives:vertexStart:vertexCount:</c>.</summary>
    /// <param name="receiver">The encoder.</param>
    /// <param name="selector">The selector.</param>
    /// <param name="primitive">The primitive type.</param>
    /// <param name="start">The first vertex.</param>
    /// <param name="count">The number of vertices.</param>
    [LibraryImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    internal static partial void SendDraw(nint receiver, nint selector, nuint primitive, nuint start, nuint count);

    /// <summary>Sends <c>replaceRegion:mipmapLevel:withBytes:bytesPerRow:</c>.</summary>
    /// <param name="receiver">The texture.</param>
    /// <param name="selector">The selector.</param>
    /// <param name="region">The region written.</param>
    /// <param name="level">The mip level.</param>
    /// <param name="bytes">The pixels.</param>
    /// <param name="bytesPerRow">The bytes per row.</param>
    [LibraryImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    internal static partial void SendReplaceRegion(nint receiver, nint selector, MetalRegion region, nuint level, void* bytes, nuint bytesPerRow);

    /// <summary>Sends <c>getBytes:bytesPerRow:fromRegion:mipmapLevel:</c>.</summary>
    /// <param name="receiver">The texture.</param>
    /// <param name="selector">The selector.</param>
    /// <param name="bytes">Receives the pixels.</param>
    /// <param name="bytesPerRow">The bytes per row.</param>
    /// <param name="region">The region read.</param>
    /// <param name="level">The mip level.</param>
    [LibraryImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    internal static partial void SendGetBytes(nint receiver, nint selector, void* bytes, nuint bytesPerRow, MetalRegion region, nuint level);

    /// <summary>Sends <c>setClearColor:</c>.</summary>
    /// <param name="receiver">The colour attachment.</param>
    /// <param name="selector">The selector.</param>
    /// <param name="color">The colour.</param>
    [LibraryImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    internal static partial void SendClearColor(nint receiver, nint selector, MetalClearColor color);

    /// <summary>Sends <c>setDrawableSize:</c>.</summary>
    /// <param name="receiver">The layer.</param>
    /// <param name="selector">The selector.</param>
    /// <param name="size">The size in pixels.</param>
    [LibraryImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    internal static partial void SendSize(nint receiver, nint selector, MetalSize size);

    /// <summary>Sends <c>stringWithUTF8String:</c>.</summary>
    /// <param name="receiver">The NSString class.</param>
    /// <param name="selector">The selector.</param>
    /// <param name="text">The null terminated UTF-8 text.</param>
    /// <returns>The autoreleased string.</returns>
    [LibraryImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    internal static partial nint SendText(nint receiver, nint selector, byte* text);

    /// <summary>Native <c>objc_autoreleasePoolPush</c>.</summary>
    /// <returns>The pool token.</returns>
    [LibraryImport(ObjectiveC, EntryPoint = "objc_autoreleasePoolPush")]
    internal static partial nint AutoreleasePoolPush();

    /// <summary>Native <c>objc_autoreleasePoolPop</c>.</summary>
    /// <param name="pool">The pool token.</param>
    [LibraryImport(ObjectiveC, EntryPoint = "objc_autoreleasePoolPop")]
    internal static partial void AutoreleasePoolPop(nint pool);
}
