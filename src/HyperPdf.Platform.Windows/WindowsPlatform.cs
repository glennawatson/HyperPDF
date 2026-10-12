// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Platform;
using HyperPdf.Core.Speech;
using HyperPdf.Platform.Windows.Audio;
using HyperPdf.Platform.Windows.Printing;
using HyperPdf.Platform.Windows.Shell;
using HyperPdf.Platform.Windows.Theme;

namespace HyperPdf.Platform.Windows;

/// <summary>
/// The Windows desktop: light or dark mode and accent colour, File Explorer, the Jump List, one window per user through
/// a named pipe, printing through the spooler and the Windows print dialog, and sound through WASAPI.
/// </summary>
[DebuggerDisplay("WindowsPlatform: {Name}")]
public sealed class WindowsPlatform : IDesktopPlatform
{
    /// <summary>The application name, used for the pipe and the data folder.</summary>
    private const string ApplicationName = "HyperPDF";

    /// <summary>Lets any process take the foreground, so the running window can come to the front (ASFW_ANY).</summary>
    private const uint AnyProcess = uint.MaxValue;

    /// <summary>The pipe and mutex name for this user.</summary>
    private static readonly string InstanceName = PipeSingleInstance.NameFor(ApplicationName);

    /// <summary>Initializes a new instance of the <see cref="WindowsPlatform"/> class.</summary>
    /// <param name="engine">The engine that draws pages for printing.</param>
    public WindowsPlatform(IDocumentEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        Printer = new WindowsPrintService(engine);
        RecentDocuments = new WindowsRecentDocumentStore(Path.Combine(DataDirectory, "recent.json"));
    }

    /// <summary>Gets the app's data folder, <c>%LOCALAPPDATA%\HyperPDF</c>.</summary>
    public static string DataDirectory { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ApplicationName);

    /// <inheritdoc/>
    public string Name => "Windows";

    /// <inheritdoc/>
    public IDesktopThemeSource? ThemeSource { get; } = new WindowsThemeSource();

    /// <inheritdoc/>
    public IFileManagerLauncher FileManager { get; } = new ExplorerFileManagerLauncher();

    /// <inheritdoc/>
    public IRecentDocumentStore RecentDocuments { get; }

    /// <inheritdoc/>
    public IPrintService Printer { get; }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IAudioOutput CreateAudioOutput() => new WasapiAudioOutput();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string? GetLaunchActivationToken() => null;

    /// <inheritdoc/>
    public Task<bool> TryForwardAsync(OpenRequest request)
    {
        // The running window may only take the foreground if this launch, which has it, allows it.
        _ = NativeMethods.AllowSetForegroundWindow(AnyProcess);
        return PipeSingleInstance.TryForwardAsync(InstanceName, request);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<ISingleInstance?> TryClaimSingleInstanceAsync() => Task.FromResult<ISingleInstance?>(PipeSingleInstance.TryStart(InstanceName));
}
