#!/usr/bin/env -S dotnet run --file
#:property TargetFrameworks=net11.0-windows
#:property UseWPF=true
#:property AllowUnsafeBlocks=true
#:property IsAotCompatible=false
#:property PublishAot=false
#:include UiaNative.cs

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// A headless stand-in for a Windows screen reader or magnifier such as JAWS or ZoomText: a UI Automation client. It starts the viewer, reads the window the way those
// programs do, sets focus on every element that can take it, and reports what each would say and whether the viewer announced the change of focus.
//   dotnet run --file src/tools/CheckUiaReader.cs -- <viewer exe> <PDF>
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Windows.Automation;

return await HyperPdf.Scripts.CheckUiaReader.RunAsync(args).ConfigureAwait(false);

namespace HyperPdf.Scripts
{
    /// <summary>Reads the viewer as a UI Automation client such as JAWS or ZoomText does, and checks that it announces every move of focus.</summary>
    internal static class CheckUiaReader
    {
        /// <summary>The milliseconds in a second.</summary>
        private const int MillisecondsPerSecond = 1000;

        /// <summary>The seconds the viewer is given to put up its window.</summary>
        private const int StartSeconds = 15;

        /// <summary>The milliseconds to wait for an announcement after focus moves.</summary>
        private const int AnnounceMilliseconds = 4000;

        /// <summary>The milliseconds between looks at whether something has happened.</summary>
        private const int PollMilliseconds = 25;

        /// <summary>The deepest level of the tree that is read.</summary>
        private const int MaximumDepth = 10;

        /// <summary>The most characters of the document's text that are printed.</summary>
        private const int TextPreview = 400;

        /// <summary>The exit code for bad arguments.</summary>
        private const int UsageExitCode = 2;

        /// <summary>The announcements the viewer made, from the automation events.</summary>
        private static readonly ConcurrentQueue<string> Announced = new();

        /// <summary>Runs the reader.</summary>
        /// <param name="args">The viewer's path and the PDF to open.</param>
        /// <returns>0 when every element that takes focus is named and announced.</returns>
        /// <exception cref="InvalidOperationException">The viewer did not start.</exception>
        internal static async Task<int> RunAsync(string[] args)
        {
            if (args.Length != UsageExitCode)
            {
                await Console.Error.WriteLineAsync("usage: CheckUiaReader.cs <viewer exe> <PDF>").ConfigureAwait(false);
                return UsageExitCode;
            }

            // Both must be existing files, and the PDF goes to the viewer as one argument, so a path with spaces stays whole.
            var viewerPath = Path.GetFullPath(args[0]);
            var pdfPath = Path.GetFullPath(args[1]);
            if (!File.Exists(viewerPath) || !File.Exists(pdfPath))
            {
                await Console.Error.WriteLineAsync("The viewer and the PDF must both be existing files.").ConfigureAwait(false);
                return UsageExitCode;
            }

            Environment.SetEnvironmentVariable("HYPERPDF_RENDER_BACKEND", "Cpu");
            var start = new ProcessStartInfo(viewerPath) { RedirectStandardError = true };
            start.ArgumentList.Add(pdfPath);
            using var viewer = Process.Start(start) ?? throw new InvalidOperationException("The viewer did notstart.");
            _ = Task.Run(async () =>
            {
                string? line;
                while ((line = await viewer.StandardError.ReadLineAsync().ConfigureAwait(false)) is not null)
                {
                    await Console.Out.WriteLineAsync($"VIEWER> {line}").ConfigureAwait(false);
                }
            });
            try
            {
                return await ReadAsync(viewer).ConfigureAwait(false);
            }
            finally
            {
                Automation.RemoveAllEventHandlers();
                viewer.Kill(true);
            }
        }

        /// <summary>Reads the viewer's window, moves focus through it and reports.</summary>
        /// <param name="viewer">The running viewer.</param>
        /// <returns>The exit code.</returns>
        private static async Task<int> ReadAsync(Process viewer)
        {
            if (await FindWindowAsync(viewer).ConfigureAwait(false) is not { } window)
            {
                await Console.Out.WriteLineAsync($"FAIL: the viewer has no window a reader can find (exited: {viewer.HasExited}).").ConfigureAwait(false);
                return 1;
            }

            await Console.Out.WriteLineAsync($"WINDOW: '{window.Current.Name}' ({window.Current.LocalizedControlType})").ConfigureAwait(false);
            Automation.AddAutomationFocusChangedEventHandler((sender, _) =>
            {
                try
                {
                    var element = (AutomationElement)sender;
                    Announced.Enqueue($"{element.Current.ProcessId}/{viewer.Id} {element.Current.LocalizedControlType}: {element.Current.Name}");
                }
                catch (Exception ex)
                {
                    Announced.Enqueue($"event failed: {ex.GetType().Name} {ex.Message}");
                }
            });

            var focusable = new List<AutomationElement>();
            Collect(window, 0, focusable);
            _ = focusable.RemoveAll(static element => Equals(element.Current.ControlType, ControlType.MenuItem) && element.Current.Name == "System");

            // The frame is announced by the operating system when the window activates, not by the viewer.
            _ = focusable.RemoveAll(static element => Equals(element.Current.ControlType, ControlType.Window));
            _ = focusable.RemoveAll(static element => Equals(element.Current.ControlType, ControlType.MenuBar) && element.Current.Name == "System Menu Bar");
            var silent = 0;
            foreach (var element in focusable)
            {
                silent += await MoveFocusToAsync(element).ConfigureAwait(false) ? 0 : 1;
            }

            foreach (var seen in Announced)
            {
                await Console.Out.WriteLineAsync($"EVENT> {seen}").ConfigureAwait(false);
            }

            await ReadDocumentAsync(window).ConfigureAwait(false);
            var unnamed = 0;
            foreach (var element in focusable)
            {
                unnamed += string.IsNullOrWhiteSpace(element.Current.Name) ? 1 : 0;
            }

            await Console.Out.WriteLineAsync($"{focusable.Count} elements take focus; {silent} were not announced; {unnamed} have no name.").ConfigureAwait(false);
            return silent == 0 && unnamed == 0 && focusable.Count > 0 ? 0 : 1;
        }

        /// <summary>Finds the viewer's window the way a reader does, once the viewer has put one up.</summary>
        /// <param name="process">The viewer.</param>
        /// <returns>The window, or <see langword="null"/> when none appeared.</returns>
        private static async Task<AutomationElement?> FindWindowAsync(Process process)
        {
            for (var waited = 0; waited < StartSeconds * MillisecondsPerSecond; waited += PollMilliseconds)
            {
                // The desktop's own list of windows is empty in a session nobody sits at, so the window is looked up by its process too.
                var listed = AutomationElement.RootElement.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.ProcessIdProperty, process.Id));
                if (listed is not null)
                {
                    return listed;
                }

                if (UiaNative.FindWindowOf(process.Id) is var handle && handle != IntPtr.Zero)
                {
                    return AutomationElement.FromHandle(handle);
                }

                await Task.Delay(PollMilliseconds).ConfigureAwait(false);
            }

            return null;
        }

        /// <summary>Walks the control view, which is what a screen reader navigates, collecting the elements that take focus.</summary>
        /// <param name="element">The element to start at.</param>
        /// <param name="depth">How deep it is.</param>
        /// <param name="focusable">Receives the elements that take focus, in tree order.</param>
        private static void Collect(AutomationElement element, int depth, List<AutomationElement> focusable)
        {
            if (depth > MaximumDepth)
            {
                return;
            }

            if (element.Current.IsKeyboardFocusable)
            {
                focusable.Add(element);
            }

            for (var child = TreeWalker.ControlViewWalker.GetFirstChild(element); child is not null; child = TreeWalker.ControlViewWalker.GetNextSibling(child))
            {
                Collect(child, depth + 1, focusable);
            }
        }

        /// <summary>Sets focus on an element and waits to see whether the viewer announced it.</summary>
        /// <param name="element">The element.</param>
        /// <returns><see langword="true"/> when focus was taken and announced, or was already there.</returns>
        private static async Task<bool> MoveFocusToAsync(AutomationElement element)
        {
            var already = element.Current.HasKeyboardFocus;
            var before = Announced.Count;
            try
            {
                element.SetFocus();
            }
            catch (InvalidOperationException)
            {
                await Console.Out.WriteLineAsync($"SILENT> {element.Current.LocalizedControlType}: '{element.Current.Name}' (focus refused)").ConfigureAwait(false);
                return false;
            }

            var evented = !already && await WaitForAsync(() => Announced.Count > before).ConfigureAwait(false);

            // A magnifier follows the element UI Automation says has focus, so that is checked as well as the event.
            var tracked = element.Current.HasKeyboardFocus || Automation.Compare(AutomationElement.FocusedElement, element);
            var verdict = evented || already;
            await Console.Out.WriteLineAsync(Format(element, verdict, Describe(already, evented, tracked))).ConfigureAwait(false);
            return verdict;
        }

        /// <summary>Describes how a move of focus showed.</summary>
        /// <param name="already">Whether the element already had focus.</param>
        /// <param name="evented">Whether a focus event arrived.</param>
        /// <param name="tracked">Whether UI Automation reports the element as focused.</param>
        /// <returns>The words.</returns>
        private static string Describe(bool already, bool evented, bool tracked)
        {
            if (already)
            {
                return "already focused";
            }

            if (evented)
            {
                return "focus moved, event seen";
            }

            return tracked ? "focus moved, no event seen" : "focus did not move";
        }

        /// <summary>Makes the line printed for an element.</summary>
        /// <param name="element">The element.</param>
        /// <param name="verdict">Whether it was read.</param>
        /// <param name="how">How focus showed.</param>
        /// <returns>The line.</returns>
        private static string Format(AutomationElement element, bool verdict, string how) =>
            $"{(verdict ? "READER" : "SILENT")}> {element.Current.LocalizedControlType}: '{element.Current.Name}' ({how}) patterns: {Patterns(element)}";

        /// <summary>Lists the patterns an element offers, by name.</summary>
        /// <param name="element">The element.</param>
        /// <returns>The names, or a dash when there are none.</returns>
        private static string Patterns(AutomationElement element)
        {
            var names = new List<string>();
            foreach (var pattern in element.GetSupportedPatterns())
            {
                names.Add(pattern.ProgrammaticName.Replace("PatternIdentifiers.Pattern", string.Empty, StringComparison.Ordinal));
            }

            return names.Count == 0 ? "-" : string.Join(", ", names);
        }

        /// <summary>Prints what a reader says for the page: the document's name and the text it finds under it.</summary>
        /// <param name="root">The window.</param>
        /// <returns>A task that ends when the lines are printed.</returns>
        private static async Task ReadDocumentAsync(AutomationElement root)
        {
            var document = root.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Document));
            if (document is null)
            {
                await Console.Out.WriteLineAsync("DOCUMENT: none found").ConfigureAwait(false);
                return;
            }

            await Console.Out.WriteLineAsync($"DOCUMENT: '{document.Current.Name}'").ConfigureAwait(false);
            await PrintPatternsAsync(document).ConfigureAwait(false);
            if (document.TryGetCurrentPattern(TextPattern.Pattern, out var pattern))
            {
                await Console.Out.WriteLineAsync($"DOCUMENT TEXT (Text pattern): '{((TextPattern)pattern).DocumentRange.GetText(TextPreview)}'").ConfigureAwait(false);
                return;
            }

            foreach (AutomationElement text in document.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text)))
            {
                await Console.Out.WriteLineAsync($"DOCUMENT TEXT (child): '{text.Current.Name}'").ConfigureAwait(false);
            }
        }

        /// <summary>Prints the patterns an element offers, which is how a reader decides how to read it.</summary>
        /// <param name="element">The element.</param>
        /// <returns>A task that ends when the lines are printed.</returns>
        private static async Task PrintPatternsAsync(AutomationElement element)
        {
            foreach (var supported in element.GetSupportedPatterns())
            {
                await Console.Out.WriteLineAsync($"DOCUMENT PATTERN: {supported.ProgrammaticName}").ConfigureAwait(false);
            }

            var available = element.GetCurrentPropertyValue(AutomationElement.IsTextPatternAvailableProperty);
            await Console.Out.WriteLineAsync($"DOCUMENT IsTextPatternAvailable: {available}").ConfigureAwait(false);
        }

        /// <summary>Waits for a condition for a short time.</summary>
        /// <param name="condition">The condition.</param>
        /// <returns><see langword="true"/> when it held in time.</returns>
        private static async Task<bool> WaitForAsync(Func<bool> condition)
        {
            for (var waited = 0; waited < AnnounceMilliseconds; waited += PollMilliseconds)
            {
                if (condition())
                {
                    return true;
                }

                await Task.Delay(PollMilliseconds).ConfigureAwait(false);
            }

            return condition();
        }
    }
}
