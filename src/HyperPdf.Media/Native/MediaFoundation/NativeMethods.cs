// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Media.Native.MediaFoundation;

/// <summary>Source generated entry points of the Windows Media Foundation and COM functions the video decoder uses, all part of Windows itself.</summary>
internal static unsafe partial class NativeMethods
{
    /// <summary>Native <c>CoInitializeEx</c>.</summary>
    /// <param name="reserved">Must be null.</param>
    /// <param name="flags">The threading model.</param>
    /// <returns>An HRESULT.</returns>
    [LibraryImport("ole32.dll", EntryPoint = "CoInitializeEx")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static partial int CoInitializeEx(nint reserved, uint flags);

    /// <summary>Native <c>CoTaskMemFree</c>.</summary>
    /// <param name="memory">The memory to free.</param>
    [LibraryImport("ole32.dll", EntryPoint = "CoTaskMemFree")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static partial void CoTaskMemFree(void* memory);

    /// <summary>Native <c>MFStartup</c>.</summary>
    /// <param name="version">The version of Media Foundation the caller was written for.</param>
    /// <param name="flags">What to start; 0 for all.</param>
    /// <returns>An HRESULT.</returns>
    [LibraryImport("mfplat.dll", EntryPoint = "MFStartup")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static partial int MFStartup(uint version, uint flags);

    /// <summary>Native <c>MFShutdown</c>.</summary>
    /// <returns>An HRESULT.</returns>
    [LibraryImport("mfplat.dll", EntryPoint = "MFShutdown")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static partial int MFShutdown();

    /// <summary>Native <c>MFCreateMediaType</c>.</summary>
    /// <param name="type">Receives the empty media type.</param>
    /// <returns>An HRESULT.</returns>
    [LibraryImport("mfplat.dll", EntryPoint = "MFCreateMediaType")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static partial int MFCreateMediaType(void** type);

    /// <summary>Native <c>MFCreateSample</c>.</summary>
    /// <param name="sample">Receives the empty sample.</param>
    /// <returns>An HRESULT.</returns>
    [LibraryImport("mfplat.dll", EntryPoint = "MFCreateSample")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static partial int MFCreateSample(void** sample);

    /// <summary>Native <c>MFCreateMemoryBuffer</c>.</summary>
    /// <param name="maxLength">The size of the buffer in bytes.</param>
    /// <param name="buffer">Receives the buffer.</param>
    /// <returns>An HRESULT.</returns>
    [LibraryImport("mfplat.dll", EntryPoint = "MFCreateMemoryBuffer")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static partial int MFCreateMemoryBuffer(uint maxLength, void** buffer);

    /// <summary>Native <c>MFTEnumEx</c>.</summary>
    /// <param name="category">The kind of transform, as a class id.</param>
    /// <param name="flags">Which transforms to list.</param>
    /// <param name="input">The input type wanted, or null.</param>
    /// <param name="output">The output type wanted, or null.</param>
    /// <param name="activates">Receives an array of activation objects, to be freed with <see cref="CoTaskMemFree"/>.</param>
    /// <param name="count">Receives the number of activation objects.</param>
    /// <returns>An HRESULT.</returns>
    [LibraryImport("mfplat.dll", EntryPoint = "MFTEnumEx")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static partial int MFTEnumEx(Guid category, uint flags, RegisterTypeInfo* input, RegisterTypeInfo* output, void*** activates, uint* count);
}
