// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Media.Native.VaApi;

/// <summary>Source generated entry points of the VA-API functions the video decoder uses. VA-API is the video acceleration interface of Linux graphics drivers.</summary>
internal static unsafe partial class NativeMethods
{
    /// <summary>The name of the C library.</summary>
    internal const string CLibrary = "libc.so.6";

    /// <summary>The name of the VA-API library.</summary>
    internal const string Library = "libva.so.2";

    /// <summary>The name of the VA-API library that opens a display from a DRM device.</summary>
    internal const string DrmLibrary = "libva-drm.so.2";

    /// <summary>Native <c>open</c>, which opens the DRM render node a VA-API display is made on.</summary>
    /// <param name="path">The path of the device file.</param>
    /// <param name="flags">The open flags.</param>
    /// <returns>A file descriptor, or -1 when the file could not be opened.</returns>
    [LibraryImport(CLibrary, EntryPoint = "open", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static partial int OpenDevice(string path, int flags);

    /// <summary>Native <c>close</c>.</summary>
    /// <param name="descriptor">The file descriptor.</param>
    /// <returns>0 when it was closed.</returns>
    [LibraryImport(CLibrary, EntryPoint = "close")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static partial int CloseDevice(int descriptor);

    /// <summary>Native <c>vaGetDisplayDRM</c>.</summary>
    /// <param name="descriptor">The file descriptor of a DRM render node.</param>
    /// <returns>The display, or 0 when none could be made.</returns>
    [LibraryImport(DrmLibrary, EntryPoint = "vaGetDisplayDRM")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static partial nint GetDisplay(int descriptor);

    /// <summary>Native <c>vaInitialize</c>.</summary>
    /// <param name="display">The display.</param>
    /// <param name="major">Receives the major version.</param>
    /// <param name="minor">Receives the minor version.</param>
    /// <returns>A VAStatus.</returns>
    [LibraryImport(Library, EntryPoint = "vaInitialize")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static partial int Initialize(nint display, int* major, int* minor);

    /// <summary>Native <c>vaTerminate</c>.</summary>
    /// <param name="display">The display.</param>
    /// <returns>A VAStatus.</returns>
    [LibraryImport(Library, EntryPoint = "vaTerminate")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static partial int Terminate(nint display);

    /// <summary>Native <c>vaCreateConfig</c>.</summary>
    /// <param name="display">The display.</param>
    /// <param name="profile">The profile.</param>
    /// <param name="entrypoint">The entry point.</param>
    /// <param name="attributes">The attributes.</param>
    /// <param name="count">The number of attributes.</param>
    /// <param name="config">Receives the configuration.</param>
    /// <returns>A VAStatus.</returns>
    [LibraryImport(Library, EntryPoint = "vaCreateConfig")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static partial int CreateConfig(nint display, int profile, int entrypoint, ConfigAttribute* attributes, int count, uint* config);

    /// <summary>Native <c>vaDestroyConfig</c>.</summary>
    /// <param name="display">The display.</param>
    /// <param name="config">The configuration.</param>
    /// <returns>A VAStatus.</returns>
    [LibraryImport(Library, EntryPoint = "vaDestroyConfig")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static partial int DestroyConfig(nint display, uint config);

    /// <summary>Native <c>vaCreateSurfaces</c>.</summary>
    /// <param name="display">The display.</param>
    /// <param name="format">The render target format.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="surfaces">Receives the surfaces.</param>
    /// <param name="count">The number of surfaces.</param>
    /// <param name="attributes">Extra attributes, or null.</param>
    /// <param name="attributeCount">The number of extra attributes.</param>
    /// <returns>A VAStatus.</returns>
    [LibraryImport(Library, EntryPoint = "vaCreateSurfaces")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static partial int CreateSurfaces(nint display, uint format, uint width, uint height, uint* surfaces, uint count, void* attributes, uint attributeCount);

    /// <summary>Native <c>vaDestroySurfaces</c>.</summary>
    /// <param name="display">The display.</param>
    /// <param name="surfaces">The surfaces.</param>
    /// <param name="count">The number of surfaces.</param>
    /// <returns>A VAStatus.</returns>
    [LibraryImport(Library, EntryPoint = "vaDestroySurfaces")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static partial int DestroySurfaces(nint display, uint* surfaces, int count);

    /// <summary>Native <c>vaCreateContext</c>.</summary>
    /// <param name="display">The display.</param>
    /// <param name="config">The configuration.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="flags">The flags; 0 for decoding.</param>
    /// <param name="targets">The surfaces pictures are decoded into.</param>
    /// <param name="count">The number of surfaces.</param>
    /// <param name="context">Receives the context.</param>
    /// <returns>A VAStatus.</returns>
    [LibraryImport(Library, EntryPoint = "vaCreateContext")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static partial int CreateContext(nint display, uint config, int width, int height, int flags, uint* targets, int count, uint* context);

    /// <summary>Native <c>vaDestroyContext</c>.</summary>
    /// <param name="display">The display.</param>
    /// <param name="context">The context.</param>
    /// <returns>A VAStatus.</returns>
    [LibraryImport(Library, EntryPoint = "vaDestroyContext")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static partial int DestroyContext(nint display, uint context);

    /// <summary>Native <c>vaCreateBuffer</c>.</summary>
    /// <param name="display">The display.</param>
    /// <param name="context">The context.</param>
    /// <param name="type">The buffer type.</param>
    /// <param name="size">The size of one element in bytes.</param>
    /// <param name="count">The number of elements.</param>
    /// <param name="data">The initial data, which is copied.</param>
    /// <param name="buffer">Receives the buffer.</param>
    /// <returns>A VAStatus.</returns>
    [LibraryImport(Library, EntryPoint = "vaCreateBuffer")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static partial int CreateBuffer(nint display, uint context, int type, uint size, uint count, void* data, uint* buffer);

    /// <summary>Native <c>vaDestroyBuffer</c>.</summary>
    /// <param name="display">The display.</param>
    /// <param name="buffer">The buffer.</param>
    /// <returns>A VAStatus.</returns>
    [LibraryImport(Library, EntryPoint = "vaDestroyBuffer")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static partial int DestroyBuffer(nint display, uint buffer);

    /// <summary>Native <c>vaBeginPicture</c>.</summary>
    /// <param name="display">The display.</param>
    /// <param name="context">The context.</param>
    /// <param name="target">The surface the picture is decoded into.</param>
    /// <returns>A VAStatus.</returns>
    [LibraryImport(Library, EntryPoint = "vaBeginPicture")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static partial int BeginPicture(nint display, uint context, uint target);

    /// <summary>Native <c>vaRenderPicture</c>.</summary>
    /// <param name="display">The display.</param>
    /// <param name="context">The context.</param>
    /// <param name="buffers">The buffers.</param>
    /// <param name="count">The number of buffers.</param>
    /// <returns>A VAStatus.</returns>
    [LibraryImport(Library, EntryPoint = "vaRenderPicture")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static partial int RenderPicture(nint display, uint context, uint* buffers, int count);

    /// <summary>Native <c>vaEndPicture</c>.</summary>
    /// <param name="display">The display.</param>
    /// <param name="context">The context.</param>
    /// <returns>A VAStatus.</returns>
    [LibraryImport(Library, EntryPoint = "vaEndPicture")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static partial int EndPicture(nint display, uint context);

    /// <summary>Native <c>vaSyncSurface</c>.</summary>
    /// <param name="display">The display.</param>
    /// <param name="surface">The surface.</param>
    /// <returns>A VAStatus.</returns>
    [LibraryImport(Library, EntryPoint = "vaSyncSurface")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static partial int SyncSurface(nint display, uint surface);

    /// <summary>Native <c>vaDeriveImage</c>.</summary>
    /// <param name="display">The display.</param>
    /// <param name="surface">The surface.</param>
    /// <param name="image">Receives the <c>VAImage</c> structure, 120 bytes, that describes memory shared with the surface.</param>
    /// <returns>A VAStatus.</returns>
    [LibraryImport(Library, EntryPoint = "vaDeriveImage")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static partial int DeriveImage(nint display, uint surface, byte* image);

    /// <summary>Native <c>vaDestroyImage</c>.</summary>
    /// <param name="display">The display.</param>
    /// <param name="image">The image.</param>
    /// <returns>A VAStatus.</returns>
    [LibraryImport(Library, EntryPoint = "vaDestroyImage")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static partial int DestroyImage(nint display, uint image);

    /// <summary>Native <c>vaMapBuffer</c>.</summary>
    /// <param name="display">The display.</param>
    /// <param name="buffer">The buffer.</param>
    /// <param name="data">Receives the address of the buffer's memory.</param>
    /// <returns>A VAStatus.</returns>
    [LibraryImport(Library, EntryPoint = "vaMapBuffer")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static partial int MapBuffer(nint display, uint buffer, void** data);

    /// <summary>Native <c>vaUnmapBuffer</c>.</summary>
    /// <param name="display">The display.</param>
    /// <param name="buffer">The buffer.</param>
    /// <returns>A VAStatus.</returns>
    [LibraryImport(Library, EntryPoint = "vaUnmapBuffer")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static partial int UnmapBuffer(nint display, uint buffer);
}
