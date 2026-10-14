# Hyper PDF Viewer

A calm PDF viewer for Linux, Windows and macOS. Read, search, annotate, fill forms, sign, protect, edit pages and print in one tabbed window. Documents stay on your computer unless you choose an online service.

HyperPDF is made for people with ADHD or autism, and it works to improve access for people with low vision. Controls are clear, stay where you left them and can be adjusted. See [Comfort and access](#comfort-and-access).

![Document tabs, toolbar actions and thumbnails](docs/images/document-window.png)

## Get started

Download a package from [GitHub releases](https://github.com/glennawatson/HyperPDF/releases).

| System | Packages |
|---|---|
| Windows (x64 and ARM64) | MSI installer, MSIX package, portable zip, and the bare `.exe`. The MSIX needs Windows 10 version 2004 or later. |
| macOS 12 or later (Apple silicon and Intel) | Disk image (`.dmg`) and app zip. |
| Linux (x64 and ARM64) | AppImage, `.deb`, `.rpm` and `.tar.gz`. Each release also has a PKGBUILD for Arch Linux (`hyperpdf-bin`). |

1. Choose **Open**, or drop a PDF onto the window.
2. Use the page box, thumbnails or bookmarks to move through it.
3. Choose **Find** to search or **Focus** for adjustable reading text.
4. Open **Tabs & settings → Preferences** for appearance and startup settings.

If a PDF needs a recipient certificate, choose your `.p12` or `.pfx` file and enter its password. RSA and elliptic-curve certificates both work. If the file has no private key, or the PDF uses an encryption method the viewer cannot read, the viewer says so. The viewer keeps that choice only while the tab is open. A wrapper PDF names its enclosed document and offers it in **Attachments**; open that file there and enter its document password if asked.

## Features

| Task | Options |
|---|---|
| Manage documents | Reorder tabs, find tabs, hover previews, restore the session and reopen closed tabs. Open files, password-protected PDFs and web URLs. Open Recent lists recent files; each reopens at the page you left. |
| Navigate | Thumbnails, bookmarks, page labels, links and back/forward history. Links to other PDFs open them in a tab. Links to other files ask first; programs never start. |
| View | Continuous, page-by-page, two-page and cover layouts. Rotate, fit page, fit width or set zoom. Ctrl+wheel or a pinch zooms around the pointer. Zoom to a dragged area, move pages with the Hand tool and auto-scroll at an adjustable speed. Split View shows two places at once; New Window opens another window. |
| Find text | Match case or whole words. Search one PDF or every PDF in a folder. |
| Read | Select and copy text, or all of a page's text. Copy an image of part of a page with Snapshot. Use caret browsing, Read Mode or presentation. |
| Inspect | Document properties, including whether a PDF is linearized, layers and attachments. Open or save attachments. A message names content the app cannot show, such as XFA forms or 3D. In a preseparated print file, each colour separation page names its colourant and printed page, and you can switch to another separation of the same page. |
| Annotate | Highlights, underlines, strike-outs, squiggles, notes, text, callouts, ink, shapes, arrows, polygons, clouds and stamps, including your own words or a picture. Move, resize, recolour and restyle comments after placing them. Undo and redo every change. |
| Review | Browse comments, filter them by text, type, colour or author, sort them, reply and set review status. Set the author name in Preferences. |
| Fill forms | Text fields, check boxes, radio buttons, lists and one-letter-per-box fields. Tab and Shift+Tab move between fields; Space ticks. On printed forms, lines, boxes and letter boxes are outlined and text snaps to them. Save filled forms. Common calculations and formats are supported. Spelling is checked with the desktop's dictionaries. |
| Sign | Type or draw a visible signature. Sign with a protected `.p12` or `.pfx` certificate (RSA or elliptic curve), or with an external signer when your key stays on a hardware token or with a signing service. The private key never reaches the app. Check signatures and timestamps. |
| Keep signatures valid | **Add Validation Data or Timestamp** stores the certificates and revocation answers a signature needs, so it can still be checked after the certificates expire. It can also add a trusted document timestamp. |
| Protect | **Protect with Password** sets a reading password and an owner password, and lets you allow or stop printing, copying, editing, forms and screen readers. Choose AES-256, AES-128 or AES-256 with change detection (AES-GCM). **Remove Protection** saves a copy with no password and no limits. |
| Manage pages | Delete, duplicate, rotate and reorder pages. Extract pages to a new file, insert another PDF before a page, or merge PDFs at the end. Undo and redo each change. |
| Redact | Mark text or areas, then **Apply Redactions** to remove what is under them for good. Choose what happens to pictures and drawings, and whether to remove hidden text, links, comments and metadata. |
| Save a copy | Optimised (smaller file), flattened (forms and markup become part of the page), PDF/A for long-term keeping, and PDF 2.0. Each copy window says what changed, or why a copy could not be made. Your open file is not changed. |
| Recognise text | Add searchable text to scanned pages. Tesseract and English come with the app; other languages download in one click when needed. |
| Measure | Distance, perimeter and area. Use the drawing's scale or enter your own. Keep measurements on the page. |
| Use the desktop | Recent documents, Show in Folder and opening files in the running window. Reload changed files automatically or when asked. |

## Focus and Read Aloud

Focus Mode lets you change the font, text size, spacing, width and page colour. It keeps your place when you return to the PDF. Reading order uses PDF tags where available. Other pages use their layout.

![Focus Mode with reading controls and sentence highlighting](docs/images/focus-mode.png)

Read Aloud offers local English voices, including Australian, British, American and Indian English. The first use asks before downloading voice files. Pause, move between sentences, change speed or voice, and resume from your saved place. Sentence and word highlighting are optional.

An optional online voice uses your own key. It sends the requested text to that service.

## Fill and sign

Choose **Fill & Sign** to type or draw a signature. Click a field to fill it, or click anywhere else to type there. Save the document when finished.

On a printed form, the Text tool outlines the lines and boxes it finds. Click one to type fitted to it; a row of letter boxes takes one letter per box. Hold Alt to place text freely.

Typing happens on the page, with no dialog. The format row sets the font, size, bold, italic, underline, alignment, colour and spacing, and changes show at once. Double-click text, or press F2 or Enter, to edit it again. Drag to move it, or drag a handle to rewrap it. **Text Properties…** sets everything in one window.

Text is saved as a standard typewriter comment, so other readers can show and edit it. Installed fonts are embedded as a subset of the letters used. A font whose licence forbids embedding is hidden unless **Offer fonts that cannot be saved in PDFs** is on in Preferences; that text is saved in the closest built-in font.

A visible signature is a mark on the page. A certificate signature lets readers check who signed and whether the file changed. Choose **Sign with Certificate** for that. Set a timestamp server in Preferences if needed.

## Print and export

![Print preview with paper, page range and duplex controls](docs/images/print-preview.png)

1. Choose **Print**, then select a printer or **Save as PDF**.
2. Choose pages, copies, colour and paper: A4, Letter, A3, A5, Legal or Tabloid. Direct printing fits pages to the paper, or prints them at actual size, shrinks only oversized pages, or uses a custom scale.
3. For two-sided printing, choose **Long edge** or **Short edge** under **Turn pages**.
4. Choose several pages per sheet, **Booklet** or **Poster** when needed.
5. Choose whether to include annotations. Filled form fields always print.

Use **Open in system printer view** for the printer's own settings. That dialog controls its own job. Linux direct printing includes a workaround for a duplex filter bug.

## Comfort and access

Preferences offers Calm, High contrast, Dark and Light themes. PDF pages can follow the interface or desktop, or use a White or Dark override. Page colours affect the screen only.

Adjust interface text size, motion and cursor blinking. Focus Mode adds reading text controls. Main actions have text labels. Messages stay until dismissed. Closing several tabs asks first.

No single theme suits everyone. Choose the appearance that works for you.

## Shortcuts

| Action | Shortcut |
|---|---|
| Open / save / save as | Ctrl+O / Ctrl+S / Ctrl+Shift+S |
| Print / print with the system dialog | Ctrl+P / Ctrl+Shift+P |
| Switch / close tab | Ctrl+Tab / Ctrl+W |
| New window | Ctrl+N |
| Find / search in a folder | Ctrl+F / Ctrl+Shift+F |
| Undo / redo | Ctrl+Z / Ctrl+Y |
| Zoom in / out / reset | Ctrl+Plus / Ctrl+Minus / Ctrl+0 |
| Back / forward | Alt+Left / Alt+Right |
| Show or hide the sidebar | F9 |
| Read Aloud | Ctrl+Shift+Y |
| Measure | Ctrl+Shift+M |
| Document properties | Alt+Enter |
| Reopen closed tabs / find a tab | Ctrl+Shift+T / Ctrl+Shift+A |
| Next / previous search match | F3 / Shift+F3 |
| Fit page / Focus Mode | Ctrl+1 / Ctrl+4 |
| Read Mode / Preferences | Ctrl+H / Ctrl+, |
| Caret browsing / presentation | F7 / Shift+F5 |
| First / last page | Home / End (Ctrl+Home / Ctrl+End anywhere) |
| Select all text on the page | Ctrl+A |
| Move the pages with any tool | Hold Space or the middle button and drag |
| Auto-scroll; then slower / faster | Ctrl+Shift+H; Down / Up |
| Finish typing / cancel typing | Ctrl+Enter or Tab / Escape |
| Bold / italic / underline while typing | Ctrl+B / Ctrl+I / Ctrl+U |
| Edit picked text | F2 or Enter |
| Leave the current mode | Escape |

## Limits

Arbitrary form JavaScript, XFA forms, multimedia, 3D and portfolio layouts are not supported; the app says when a document uses them.

Copies you save are checked and say so when something could not be made to fit, for example a PDF/A level the file cannot meet. Programs that follow older PDF rules may not open AES-256 with change detection (AES-GCM) files yet.

Spell checking uses the Windows and macOS spell checkers. On Linux it uses the installed word lists in `/usr/share/dict`, such as the `wbritish` or `wamerican` package, or a list of your own in `~/.local/share/hyperpdf/dictionaries`.

Linux uses X11 when available, then native Wayland. Native Wayland does not expose the required screen-reader tree. Use X11 for Linux screen-reader access. See [screen readers](docs/SCREEN-READERS.md) for navigation and other platform limits.

## Report a problem

Report a problem with the [bug form](https://github.com/glennawatson/HyperPDF/issues/new?template=bugs.yml). Suggest a change with the [feature form](https://github.com/glennawatson/HyperPDF/issues/new?template=features.yml).

## For developers

The PDF engine behind the viewer is published on NuGet as `HyperPdfLibrary`. See its [package page](docs/hyperpdf/PACKAGE-README.md).

## Research

The [research notes](docs/research/README.md) explain the evidence behind the viewer's accessibility choices. They link to the original sources and state the limits of the findings.

## License

[MIT](LICENSE).
