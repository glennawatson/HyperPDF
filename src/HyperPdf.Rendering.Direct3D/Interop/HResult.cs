// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Rendering.Direct3D.Interop;

/// <summary>Turns a failed HRESULT into an exception.</summary>
internal static class HResult
{
    /// <summary>Throws when an HRESULT reports a failure.</summary>
    /// <param name="result">The HRESULT.</param>
    /// <param name="action">What was being done, for the message.</param>
    /// <exception cref="InvalidOperationException">The call failed.</exception>
    internal static void Check(int result, string action)
    {
        if (result < 0)
        {
            throw new InvalidOperationException($"Direct3D could not {action} (HRESULT 0x{result:X8}).");
        }
    }
}
