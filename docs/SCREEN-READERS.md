# Screen readers

HyperPDF provides names, descriptions and keyboard access for screen readers on Windows, macOS and Linux. Use Narrator, NVDA or JAWS on Windows, VoiceOver on macOS, or Orca on Linux. Hands-on checks with these screen readers are still in progress.

## Moving around

- Tab moves along each tool row from left to right, then down the window. Shift+Tab moves back.
- The tab bar, tool bar and sidebar are navigation landmarks. The pages are the main landmark. Find is a search landmark.
- Every button says its action. Buttons with only an icon still have a name.
- List items say what they hold and their place, such as "Page 3 of 40" or "2 of 7".
- Status messages and Read Aloud progress are announced without moving focus.

## Reading pages

- The page area says the page, such as "Page 5 of 40", and gives the page's text as its value.
- Caret browsing (F7) puts a text cursor on the page, so the arrow keys move through the words.
- Focus Mode (Ctrl+4) shows the text as headings, paragraphs and lists. Where the desktop exposes heading levels, use the screen reader's heading commands. On Linux, move through the blocks and listen for the spoken heading level.
- Read Aloud reads the text in the app's own voice and marks the sentence on the page.

## Platform limits

| Desktop | Limit | Workaround |
|---|---|---|
| Linux, native Wayland | No accessibility tree reaches the screen reader. | Sign in to an X11 session, or keep `DISPLAY` set so the app uses XWayland. |
| Linux, X11 | Heading levels and list positions do not reach Orca. | Focus Mode headings and list items, and sidebar and dialog list items, say them in their description, such as "Heading level 2" or "2 of 7". |
| macOS | List positions do not reach VoiceOver. | Sidebar and dialog lists say their place in each item's description. Focus Mode lists do not. |
| All | The page has no text pattern, so a screen reader cannot move by word on the page itself. | Use Caret browsing, Focus Mode or Read Aloud. |

These limits come from the Avalonia UI framework. Related reports: AvaloniaUI/Avalonia#22376 and AvaloniaUI/Avalonia#22377.
