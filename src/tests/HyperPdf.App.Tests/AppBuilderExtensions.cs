// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia;
using ReactiveUI.Avalonia;

namespace HyperPdf.App.Tests;

/// <summary>Test helpers for the application builder.</summary>
internal static class AppBuilderExtensions
{
    /// <summary>Extension members for <see cref="AppBuilder"/>.</summary>
    /// <param name="builder">The application builder.</param>
    extension(AppBuilder builder)
    {
        /// <summary>Initialises ReactiveUI the same way the application does.</summary>
        /// <returns>The same builder.</returns>
        internal AppBuilder UseReactiveUIForTests() => builder.UseReactiveUI(static _ => { });
    }
}
