# Text layout fixture

Open `text-layout.png` in Capture Tool and choose **Extract text** for manual OCR testing.

The 1440 × 964 image contains a title, section headings, two columns, three article paragraphs (including an indented first line), a numbered list with wrapped items, and a final paragraph spanning the page. The intended result has 13 passages.

`New-TextLayoutFixture.ps1` regenerates the PNG and `text-layout.json` together on Windows using Segoe UI. The JSON contains word boxes measured while drawing the image and independently specified expected paragraphs. This keeps automated highlights aligned with the visible words.

The application grouping test checks the expected passages without a desktop. `TextLayoutFixture_GroupsParagraphsAndSelectsWrappedPassages` runs the full pane, checks copy order and paragraph breaks, selects article/list passages, and saves screenshots under `tests/CaptureTool.UiTests/TestResults/artifacts/text-layout`.

The desktop test uses the explicit `--ui-test-text-fixture` option with deterministic OCR data. Opening the PNG normally exercises the installed OCR provider instead.
