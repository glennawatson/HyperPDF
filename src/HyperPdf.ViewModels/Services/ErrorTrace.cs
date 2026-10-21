// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdf.ViewModels.Services;

/// <summary>The one place the app reports a failure that has nobody to tell, such as an error that ends a subscription.</summary>
public static class ErrorTrace
{
    /// <summary>Writes a failure to the trace listeners.</summary>
    /// <param name="error">The failure.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write(Exception error) => Trace.TraceError(error.ToString());
}
