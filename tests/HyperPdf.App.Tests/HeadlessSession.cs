// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Headless;

namespace HyperPdf.App.Tests;

/// <summary>The shared headless Avalonia session, using the production app with Skia rendering so frames can be captured.</summary>
public static class HeadlessSession
{
    /// <summary>The lazily created session.</summary>
    private static readonly Lazy<HeadlessUnitTestSession> Session =
        new(static () => HeadlessUnitTestSession.StartNew(typeof(HeadlessSession), AvaloniaTestIsolationLevel.PerAssembly), LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>Gets the session.</summary>
    internal static HeadlessUnitTestSession Instance => Session.Value;

    /// <summary>Builds the application for headless use.</summary>
    /// <returns>The builder.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UseHeadless(new() { UseHeadlessDrawing = false }).UseSkia().UseReactiveUIForTests();
}
