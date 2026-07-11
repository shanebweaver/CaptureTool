# PRD: Local MCP Image Capture Server

## Summary

GitHub issue: TBD

Implementation status: initial implementation is in place. The solution now includes a Windows local stdio MCP capture server, explicit CaptureKit-backed dependency wiring through the existing capture infrastructure, the `capture_primary_monitor` tool, PNG image content responses with structured metadata, unit tests, and an SDK-based `tools/list` smoke test. Remaining work is manual validation with a real MCP host invoking the screenshot tool and displaying the returned image.

The MVP should be a Windows-only local MCP server with one tool: `capture_primary_monitor`. By default, the tool captures the primary monitor through CaptureKit, encodes the result as PNG, and returns MCP image content with small structured metadata. The server is intended for progress checks, visual confirmation, and debugging agent work. It is not a replacement for the existing user-facing CaptureTool UI.

## Source Notes

- MCP tools specification: https://modelcontextprotocol.io/specification/2025-06-18/server/tools
- MCP lifecycle specification: https://modelcontextprotocol.io/specification/2025-06-18/basic/lifecycle
- MCP transports specification: https://modelcontextprotocol.io/specification/2025-06-18/basic/transports

Important protocol constraints from the docs:

- Servers expose callable tools through the `tools` capability.
- Clients discover tools with `tools/list` and invoke them with `tools/call`.
- Tool results can return image content using base64 data and a MIME type such as `image/png`.
- Structured result data can be returned in `structuredContent`; for compatibility, the same data should also be summarized in text content.
- Local stdio transport is a good first transport because the MCP client launches the server as a subprocess and the server communicates over stdin/stdout.
- A stdio server must write only valid MCP messages to stdout; diagnostics belong on stderr.
- Human-in-the-loop visibility is recommended for sensitive tools, and screen capture is sensitive by default.

## Problem

Agents often need to prove that visible work happened: a desktop app is running, a dialog appeared, a design change rendered, or a long-running operation reached the expected state. Today, an agent can describe progress but cannot always provide a fresh visual artifact without separate tooling.

CaptureTool already knows how to enumerate displays and create PNG screenshots. A small local MCP server can expose that capability to agents in a standard way while keeping capture local to the user's machine.

## Goals

- Add a local MCP server that agents can run as a subprocess.
- Expose a single MVP tool that captures the primary monitor by default.
- Return the captured image directly in the MCP tool response as PNG image content.
- Include structured metadata: width, height, monitor bounds, DPI, timestamp, and whether the monitor was primary.
- Reuse the existing Windows capture stack where practical.
- Keep the MVP explicit, auditable, and easy to disable.

## Non-Goals

- Remote or network-accessible capture.
- Region, window, all-monitor, video, or cursor capture in the MVP.
- Editing, annotation, OCR, or image analysis.
- Background scheduled capture.
- Uploading images to any external service.
- Capturing protected content that Windows intentionally blocks.
- Building a full MCP host or client.

## User Stories

- As a user, I can configure an agent to run a local CaptureTool MCP server.
- As a user, I can ask the agent for visual proof of current progress.
- As an agent, I can call one tool and receive the primary monitor screenshot as image content.
- As a user, I can see that a screenshot was captured and when it was captured.
- As a developer, I can test the capture path without launching the full CaptureTool UI.

## Current Architecture

- `IScreenCapture` in `src/CaptureTool.Application.Abstractions/Capture/IScreenCapture.cs` exposes display enumeration, monitor bitmap creation, cropping, and PNG save behavior.
- `WindowsScreenCapture` in `src/CaptureTool.Infrastructure.Capture.Windows/WindowsScreenCapture.cs` adapts the CaptureKit display capture service to `IScreenCapture`.
- `MonitorCaptureHelper` in `src/CaptureTool.Infrastructure.Capture.Windows/MonitorCaptureHelper.cs` already shows direct use of `CaptureKit.Windows.DisplayCaptureService`.
- `MonitorCaptureResult` in `src/CaptureTool.Domain.Capture/MonitorCaptureResult.cs` includes monitor handle, pixel buffer, DPI, bounds, work area, scale, and `IsPrimary`.
- `ImageCaptureWorkflow` already uses the screen-capture abstraction to write PNG files into the app temporary folder.

The MCP server can use CaptureKit directly. The preferred shape is still to keep a thin app-facing adapter, such as `IScreenCapture` or a new MCP-specific capture service, so the tool logic can be tested without requiring a real desktop capture. It does not need the existing image edit workflow, selection overlay, recent-capture post-processing, or WinUI shell.

## MVP Tool Contract

### Tool

Name: `capture_primary_monitor`

Title: `Capture Primary Monitor`

Description: Captures the current image of the primary monitor on the local Windows desktop and returns it as a PNG image for user-visible progress verification.

Input schema:

```json
{
  "type": "object",
  "properties": {
    "reason": {
      "type": "string",
      "description": "Short explanation of why the agent is requesting the capture."
    }
  },
  "additionalProperties": false
}
```

`reason` is optional in the protocol, but clients and agent instructions should prefer to send it. The server should log it to stderr for local audit visibility.

### Successful Result

The server returns:

- A short text content item summarizing the capture.
- One image content item containing base64 PNG data with `mimeType: "image/png"`.
- `structuredContent` containing capture metadata.

Example shape:

```json
{
  "content": [
    {
      "type": "text",
      "text": "Captured primary monitor at 2026-07-11T18:42:31.123Z, 2560x1440 PNG."
    },
    {
      "type": "image",
      "data": "<base64-png>",
      "mimeType": "image/png"
    }
  ],
  "structuredContent": {
    "capturedAtUtc": "2026-07-11T18:42:31.123Z",
    "width": 2560,
    "height": 1440,
    "dpi": 144,
    "scale": 1.5,
    "monitorBounds": {
      "x": 0,
      "y": 0,
      "width": 2560,
      "height": 1440
    },
    "workAreaBounds": {
      "x": 0,
      "y": 0,
      "width": 2560,
      "height": 1392
    },
    "isPrimary": true,
    "format": "png"
  },
  "isError": false
}
```

### Error Result

Tool execution errors should return `isError: true` with text content. Expected MVP error cases:

- No monitors are available.
- No monitor is marked primary.
- Capture service fails.
- PNG encoding fails.
- The server is running on an unsupported OS.

Protocol errors should be reserved for invalid MCP messages or unknown tools.

## Requirements

### Functional Requirements

1. The server exposes the MCP `tools` capability.
2. The server registers exactly one MVP tool, `capture_primary_monitor`.
3. Calling the tool enumerates monitors and selects the first monitor where `IsPrimary` is true.
4. If no primary monitor is found, the tool returns a tool execution error.
5. The tool captures the selected monitor at its native pixel size.
6. The tool encodes the result as PNG.
7. The tool returns image content in the MCP response instead of requiring the user to open a file.
8. The response includes structured metadata about the capture.
9. Diagnostic logging goes to stderr, not stdout.
10. The server exits cleanly when stdin closes.

### Technical Requirements

1. Add a new console project, likely `src/CaptureTool.Mcp.CaptureServer`.
2. Target Windows with the same .NET baseline as the rest of the solution.
3. Use stdio transport for the first implementation.
4. Use the official C# MCP SDK if it supports the required protocol version and image content shape cleanly; otherwise isolate JSON-RPC protocol handling behind a small adapter so it can be replaced later.
5. Register CaptureKit in the server's service collection, either directly with `CaptureKit.Windows.DisplayCaptureService` or through the existing `WindowsScreenCapture` adapter.
6. Implement a small application service, for example `PrimaryMonitorImageCaptureTool`, that depends on `IScreenCapture`.
7. Encode the captured `Bitmap` to PNG in memory with `MemoryStream` so the result can be returned directly as base64.
8. Dispose `Bitmap` and stream resources promptly after each tool call.
9. Keep stdout reserved for MCP JSON-RPC messages only.
10. Unit test primary-monitor selection and metadata mapping with fake `IScreenCapture`.
11. Add an integration or smoke test that verifies `tools/list` includes `capture_primary_monitor`.
12. Do not add a network listener in the MVP.

### Security and Privacy Requirements

1. The server is opt-in: it only runs when explicitly configured by the user or launched by an MCP host.
2. The default transport is stdio, not HTTP.
3. The server captures only the primary monitor in the MVP.
4. The tool description clearly says it captures the local desktop.
5. The optional `reason` argument is logged for audit visibility.
6. The server should write a concise stderr line for each capture with timestamp, dimensions, and reason.
7. No captures are persisted by default.
8. No captured bytes are sent anywhere except through the MCP response to the invoking client.
9. Future HTTP support must bind only to localhost, validate `Origin`, and add authentication before use.

## Proposed Implementation

### Phase 1: Project and MCP Shell

- Add `CaptureTool.Mcp.CaptureServer` as a console project.
- Reference the application abstractions, domain capture project, and Windows capture infrastructure.
- Reference the CaptureKit package/project used by the existing Windows capture infrastructure.
- Wire dependency injection for `IScreenCapture` or a small MCP-specific wrapper around CaptureKit.
- Add a minimal MCP server entry point over stdio.
- Implement initialize, tool listing, and tool call handling through the selected MCP SDK or an internal adapter.

### Phase 2: Primary Monitor Capture Tool

- Add `PrimaryMonitorCaptureRequest` with optional `Reason`.
- Add `PrimaryMonitorCaptureMetadata`.
- Implement monitor selection:
  - call `IScreenCapture.CaptureAllMonitors()`
  - choose the first `monitor.IsPrimary`
  - fail if no primary monitor exists
- Create a bitmap with `CreateBitmapFromMonitorCaptureResult`.
- Encode the bitmap as PNG into memory.
- Return MCP text, image, and structured content.

### Phase 3: Tests and Local Verification

- Unit test:
  - the primary monitor is selected when multiple monitors exist
  - missing primary monitor returns a tool error
  - metadata reflects monitor bounds, work area, DPI, scale, and PNG dimensions
  - capture failures become tool execution errors
- Protocol smoke test:
  - launch the server process
  - send `initialize`
  - send `tools/list`
  - verify `capture_primary_monitor` appears
- Manual verification:
  - configure the local MCP host to launch the server
  - invoke the tool
  - confirm the returned image appears in the agent conversation

## Acceptance Criteria

- The solution includes a local MCP capture server project.
- `tools/list` exposes `capture_primary_monitor`.
- Calling `capture_primary_monitor` captures the primary monitor by default.
- The tool response includes a PNG image content item.
- The tool response includes structured metadata for the capture.
- No screenshot file is written unless a later feature explicitly asks for persistence.
- The server logs diagnostics to stderr only.
- Tests cover monitor selection, error handling, and metadata mapping.
- A manual MCP host configuration can call the tool and display the returned image.

## Open Questions

- Should the server use the official C# MCP SDK immediately, or should the first PR isolate the protocol behind an adapter until the SDK dependency and version are confirmed?
- Should the tool require a non-empty `reason`, or keep it optional for compatibility with simpler clients?
- Should users be able to configure a maximum returned image size to avoid large context payloads?
- Should the MVP include cursor capture if the underlying capture stack supports it?
- Should a later version add `capture_all_monitors`, `capture_region`, or `capture_window` tools?
- Should the CaptureTool UI expose an explicit "MCP server running" status in the future?

## Risks

- Screen capture is sensitive; accidental use could expose private information.
- Some MCP hosts may not display image content consistently.
- Large high-DPI monitors can produce large base64 responses.
- Windows may block protected or elevated content from capture.
- Stdio protocol handling is strict: any accidental stdout logging can corrupt the MCP session.
- Tests can validate protocol and selection behavior, but visual capture still needs manual desktop validation.

## Tracking Checklist

- [x] Confirm MCP SDK dependency and protocol version support.
- [x] Add the MCP server console project.
- [x] Wire dependency injection for Windows screen capture.
- [x] Implement `capture_primary_monitor`.
- [x] Return PNG image content and structured metadata.
- [x] Add unit tests for selection, metadata, and error handling.
- [x] Add protocol smoke test for `initialize` and `tools/list`.
- [ ] Manually verify with a local MCP host.
