# Fill & Sign notes

Visible signatures are marks on a page. Certificate signatures help readers check the signer and file integrity. Keep these tasks distinct.

- Keep Type, Draw and Image visible. Use a stable flow: choose, preview, place, save.
- Provide keyboard placement, movement, resizing and deletion. Escape cancels placement.
- Offer paper removal with a preview and a way to keep the original. Preserve transparency and soft ink edges. Trim empty margins.
- Save smooth drawn curves in the PDF. Check the reopened and printed result.
- Make signature, initials and certificate reuse optional. Allow each saved item to be forgotten. Never store passwords or private keys in JSON settings.

The [accessibility research](research/README.md) guides labels, choice and keyboard access. White-paper removal uses the [source-over compositing equation](https://www.w3.org/TR/compositing-1/#simplealphacompositing). It assumes white paper. Shadows and off-white paper may not disappear cleanly.

Signing work is tracked in [form signing controls](https://github.com/glennawatson/HyperPDF/issues/7) and [optional signing identity reuse](https://github.com/glennawatson/HyperPDF/issues/8).

## Typing on the page

Adobe's [fill and sign guide](https://helpx.adobe.com/au/acrobat/desktop/work-with-pdf-forms/fill-sign-forms/fill-sign.html) sets the model: click to type, a small format row, and fields snapped to the form. A dialog only adds steps, so typing starts with a caret and the dialog stays for advanced properties.

- Save typed text as FreeText with `/IT /FreeTextTypeWriter`, `/DA`, `/DS` and `/RC` ([ISO 32000-1, 12.5.6.6](https://opensource.adobe.com/dc-acrobat-sdk-docs/pdfstandards/PDF32000_2008.pdf)). Draw the appearance from text runs, so readers without rich-text support show the same result.
- Embed installed fonts as subsets. Honour the OpenType [`fsType`](https://learn.microsoft.com/en-us/typography/opentype/spec/os2#fstype) bits: restricted and bitmap-only fonts are never embedded, and no-subsetting fonts are not cut down. Showing a restricted font on screen is opt-in. Its text is saved in the closest built-in font.
- Comb fields use field flag bit 25 with `/MaxLen` (12.7.4.3). Printed letter boxes get the same spacing.
- Flat forms are read from a 2× render. Drawn rules become lines, boxes and letter-box rows. Faded, grainy and soft scans are read, and pages turned up to about a degree. Pages turned further, or with broken lines, may be missed. Alt-click places text freely, so a missed place never blocks typing.
- The preview uses the same layout as the saved text: CSS line boxes, HarfBuzz shaping and kerning. A test compares the preview's ink with the saved page's ink.
