// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Head.Windows.Accessibility;
using HyperPdf.Head.Windows.Win32;
using HyperPdf.Presentation.Input;
using HyperPdf.Windowing.Rendering;
using HyperPdf.Windowing.Windows;

namespace HyperPdf.Windowing.Tests;

/// <summary>Opens a real Win32 window. The tests skip on other operating systems.</summary>
public sealed class Win32WindowTests
{
    /// <summary>The white used to clear the frame.</summary>
    private const uint White = 0xFFFFFFFF;

    /// <summary>The UI Automation Name property id.</summary>
    private const int NameProperty = 30_005;

    /// <summary>The UI Automation ControlType property id.</summary>
    private const int ControlTypeProperty = 30_003;

    /// <summary>The UI Automation control type id of a window.</summary>
    private const int WindowControlType = 50_032;

    /// <summary>The WM_CHAR message.</summary>
    private const uint CharacterMessage = 0x0102;

    /// <summary>The character posted to the window, the letter é.</summary>
    private const nuint TypedCharacter = 0xE9;

    /// <summary>The text put on the clipboard.</summary>
    private const string ClipboardSample = "HyperPDF clipboard check ✓";

    /// <summary>How long to wait for events in the test, in milliseconds.</summary>
    private const int WaitMilliseconds = 100;

    /// <summary>A window opens, shows a frame, handles messages and closes cleanly.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WindowOpensPresentsAFrameAndCloses()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip.Test("Win32 windows only run on Windows.");
            return;
        }

        var bridge = new RecordingAccessibilityBridge();
        using (var window = new Win32Window(RenderBackendPreference.Gpu, bridge))
        {
            window.SetTitle("HyperPDF window test");
            window.SetCursor(ViewerCursor.Hand);
            window.Show();
            using (var pass = window.Backend.BeginFrame())
            {
                pass.Clear(White);
            }

            window.PumpEvents(WaitMilliseconds, static (in _) => { });

            await Assert.That(window.ClientSize.IsEmpty).IsFalse();
            await Assert.That(window.Backend.IsAlive).IsTrue();
        }

        await Assert.That(bridge.Disposed).IsTrue();
    }

    /// <summary>Text the window copies can be read back from the Windows clipboard, and a character message becomes a typed text event.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WindowSetsTheClipboardAndReportsTypedText()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip.Test("Win32 windows only run on Windows.");
            return;
        }

        var typed = new List<uint>();
        string? copied;
        using (var window = new Win32Window(RenderBackendPreference.Cpu, new RecordingAccessibilityBridge()))
        {
            window.Show();
            window.SetClipboardText(ClipboardSample);
            copied = Win32ClipboardReader.ReadText((nint)window.WindowHandle);
            _ = NativeMethods.PostMessage((nint)window.WindowHandle, CharacterMessage, TypedCharacter, 0);
            window.PumpEvents(WaitMilliseconds, (in windowEvent) =>
            {
                if (windowEvent.Kind == WindowEventKind.TextInput)
                {
                    typed.Add(windowEvent.Code);
                }
            });
        }

        await Assert.That(copied).IsEqualTo(ClipboardSample);
        await Assert.That(typed).Contains((uint)TypedCharacter);
    }

    /// <summary>The window answers UI Automation, so a screen reader reads its name and control type from the published tree.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WindowAnswersUiAutomationFromThePublishedTree()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip.Test("UI Automation only runs on Windows.");
            return;
        }

        using var bridge = new UiaAccessibilityBridge();
        using var window = new Win32Window(RenderBackendPreference.Gpu, bridge);
        bridge.Attach(new FakeAccessibleTree());
        window.Show();
        window.PumpEvents(WaitMilliseconds, static (in _) => { });

        var name = UiaClient.ReadString(window.WindowHandle, NameProperty);
        var controlType = UiaClient.ReadInt32(window.WindowHandle, ControlTypeProperty);

        await Assert.That(name).IsEqualTo("Document window");
        await Assert.That(controlType).IsEqualTo(WindowControlType);
    }
}
