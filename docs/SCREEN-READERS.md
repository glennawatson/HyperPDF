# Screen readers

HyperPDF provides names, descriptions and keyboard access for screen readers on Windows, macOS and Linux. Use Narrator, NVDA or JAWS on Windows, VoiceOver on macOS, or Orca on Linux. Hands-on checks with these screen readers are still in progress.

## Moving around

- Tab moves along each tool row from left to right, then down the window. Shift+Tab moves back.
- The tab bar, tool bar and sidebar are navigation landmarks. The pages are the main landmark. Find is a search landmark.
- Every button says its action. Buttons with only an icon still have a name.
- List items say what they hold and their place, such as "Page 3 of 40" or "2 of 7".
- Status messages and Read Aloud progress are announced without moving focus.
- Picking an annotation with the Select tool is described on the page area, for example "Selected: Rectangle, yellow, on page 2". Delete removes it and the arrow keys move it. The Annotations list in the sidebar is the other way to pick one.
- Menus are solid, bordered panels with an accent bar on the highlighted row, so they stay clear of the page behind them.

## Reading pages

- The page area says the page, such as "Page 5 of 40", and gives the page's text as its value.
- Caret browsing (F7) puts a text cursor on the page, so the arrow keys move through the words. **Keep the keyboard cursor on pages** in Preferences starts every document this way.
- Read Aloud works from the keyboard without the bar: Ctrl+Shift+V reads the page, Ctrl+Shift+B reads to the end, Ctrl+Shift+C pauses or resumes, Ctrl+Shift+E stops, and Ctrl+Shift+Comma and Ctrl+Shift+Period move a sentence.
- Focus Mode (Ctrl+4) shows the text as headings, paragraphs and lists. Use the screen reader's heading commands to move between headings. On macOS, move through the blocks and listen for the spoken heading level.
- Read Aloud reads the text in the app's own voice and marks the sentence on the page.

## Platform limits

| Desktop | Limit | Workaround |
|---|---|---|
| macOS | List positions do not reach VoiceOver, and the page has no text interface. | Sidebar and dialog lists say their place in each item's description. Use Caret browsing, Focus Mode or Read Aloud. |

Linux and Windows have no limits. When a screen reader is on, HyperPDF serves its own elements: through an AT-SPI server on X11 and on native Wayland, and through UI Automation on Windows. Both send the full tree, heading levels, list positions, landmarks and the page text, so a screen reader can move by character, word, line and paragraph (and sentence on Linux). A failure in the Windows provider falls back to the UI framework's elements for that window. macOS still uses the Avalonia UI framework's tree, which causes the limit above. Related reports: AvaloniaUI/Avalonia#22376 and AvaloniaUI/Avalonia#22377.

## How the checks were made

- **Linux:** a private session with the accessibility bus, Xvfb or a headless Wayland compositor, and `busctl`; `src/tools/CheckScreenReader.cs` runs it.
- **Windows 11:** a UI Automation client walked the app's elements, read the page text by word and line, toggled and invoked controls, hit tested a point and received property and structure events. Narrator, NVDA and JAWS themselves have not been run.
- **macOS:** not checked.
