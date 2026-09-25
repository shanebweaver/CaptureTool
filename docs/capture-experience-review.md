# Capture experience implementation and review

Working branch: `codex/capture-details`. The three PRDs are [pane](prd-capture-experience-1-pane.md), [content](prd-capture-experience-2-content.md), and [naming](prd-capture-experience-3-naming.md).

## Step 1 review — in progress

- The local Windows property reader is shared by the analysis adapter and editor. It does not invoke models or require consent.
- Basic properties and saved analysis load independently. Metadata deletion clears optional content without removing basic properties; disposal rejects late responses from both reads.
- All editors host the same adaptive pane. Opening it is explicit; its open preference lasts across editor navigation during the app session. The Home Details dialog is removed.
- Source properties are distinguished from pending edits. Clipboard and folder errors remain inline.
- Managed checks: 426 application tests and 280 presentation tests passed, including independent loading, deletion and late local-read cases.
- Native x64 compilation passed. The first desktop run exposed a missing automation node on the pane's layout root. Visual inspection confirmed that the content rendered; an explicit pane automation peer was added. Review also removed blank status spacing and restored toolbar focus on keyboard/close-button dismissal. The isolated English native desktop flow passed after these fixes; wide light and narrow dark screenshots were inspected. Other locales will run in the final suite.
- Reopening from Home exposed a working-copy identity issue. Audio/video now retain the original file path like images, and all editors expose a stable details source independently of their working/rendered files. Existing open-file tests now assert that original-path contract.

## Step 2

Implementation in progress.

## Step 3

Not started.

## Verification limitations

Desktop UI fixtures provide deterministic layout/lifecycle coverage, not model-quality measurements. Physical ARM64, offline isolation, and spoken Narrator verification remain the previously documented environment limitations. No new model or provider is introduced by this work.
