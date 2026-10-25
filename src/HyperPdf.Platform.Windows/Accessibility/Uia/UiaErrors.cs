// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace HyperPdf.Platform.Windows.Accessibility.Uia;

/// <summary>
/// Makes the errors a provider gives UI Automation. UI Automation reads only the HRESULT an error carries, which tells it what went
/// wrong rather than only that something did, so the errors have no message.
/// </summary>
internal static class UiaErrors
{
    /// <summary>The element has gone, so UI Automation should drop it (<c>UIA_E_ELEMENTNOTAVAILABLE</c>).</summary>
    private const uint ElementNotAvailableCode = 0x80040201;

    /// <summary>The element does not support the call (<c>UIA_E_NOTSUPPORTED</c>).</summary>
    private const uint NotSupportedCode = 0x80040204;

    /// <summary>Tells <see cref="Marshal.GetExceptionForHR(int, nint)"/> to ignore the thread's error information.</summary>
    private static readonly nint IgnoreErrorInfo = -1;

    /// <summary>Makes the error for an element that has gone. The generated COM code turns the error into its HRESULT.</summary>
    /// <returns>The error.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Exception ElementGone() => Marshal.GetExceptionForHR(unchecked((int)ElementNotAvailableCode), IgnoreErrorInfo)!;

    /// <summary>Makes the error for a call the element does not support. The generated COM code turns the error into its HRESULT.</summary>
    /// <returns>The error.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Exception Unsupported() => Marshal.GetExceptionForHR(unchecked((int)NotSupportedCode), IgnoreErrorInfo)!;
}
