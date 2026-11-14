// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using System.Runtime.Versioning;

[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.System32)]

namespace HyperPdf.Windowing.Tests;

/// <summary>Entry points of the UI Automation core library that the test client uses.</summary>
[SupportedOSPlatform("windows")]
internal static unsafe partial class NativeMethods
{
    /// <summary>The UI Automation core library.</summary>
    private const string UiaCore = "uiautomationcore";

    /// <summary>Native <c>UiaNodeFromHandle</c>.</summary>
    /// <param name="window">The window.</param>
    /// <param name="node">Receives the node.</param>
    /// <returns>An HRESULT.</returns>
    [LibraryImport(UiaCore, EntryPoint = "UiaNodeFromHandle")]
    internal static partial int NodeFromHandle(nint window, out nint node);

    /// <summary>Native <c>UiaGetPropertyValue</c>.</summary>
    /// <param name="node">The node.</param>
    /// <param name="propertyId">The property id.</param>
    /// <param name="value">Receives the VARIANT.</param>
    /// <returns>An HRESULT.</returns>
    [LibraryImport(UiaCore, EntryPoint = "UiaGetPropertyValue")]
    internal static partial int GetPropertyValue(nint node, int propertyId, byte* value);

    /// <summary>Native <c>UiaNodeRelease</c>.</summary>
    /// <param name="node">The node.</param>
    /// <returns>Nonzero when released.</returns>
    [LibraryImport(UiaCore, EntryPoint = "UiaNodeRelease")]
    internal static partial int NodeRelease(nint node);
}
