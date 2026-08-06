# PRD: Mixed-DPI MCP Capture Metadata

- Issue: [#428](https://github.com/shanebweaver/CaptureTool/issues/428)
- Architecture finding: `ARCH-23`
- Severity: Medium
- Status: Implemented
- Affected features: `MCP-03`, `MCP-04`

## Summary

MCP captures that combine pixels from multiple monitors must not present one monitor's DPI and scale as if those scalar values described the entire image. Mixed-DPI results will expose nullable scalar DPI/scale values, an explicit uniformity indicator, and a per-monitor segment map relating virtual-screen source coordinates to image coordinates.

## Problem

All-screens capture currently selects the primary monitor as a metadata reference. A spanning region selects the monitor with the largest intersection. Both then publish that monitor's DPI and scale for a composite PNG, even when another part of the image came from a monitor with different scaling.

The scalar metadata appears authoritative, so an MCP client can apply the wrong conversion when locating or annotating content on another monitor. The response also omits the segment geometry needed to determine which scale applies to a given image coordinate.

## Goals

1. Preserve scalar DPI and scale for single-monitor and uniform-DPI captures.
2. Return null scalar DPI and scale for mixed-DPI composite captures.
3. Expose whether scalar DPI/scale is uniform across the image.
4. Include every contributing monitor's source bounds, image bounds, DPI, scale, ID, and primary status.
5. Preserve the segment map when creating an annotated derivative.
6. Keep existing capture IDs, image bytes, coordinate spaces, and uniform-capture behavior compatible.

## Non-goals

- Resampling monitor pixels into a single logical-DPI coordinate system.
- Changing Windows monitor enumeration or bitmap-combination behavior.
- Changing window-capture DPI semantics, which are outside the two composite capture paths identified by ARCH-23.
- Adding monitor names or persistent monitor identifiers.

## Metadata contract

### Uniform capture

- `dpi` and `scale` contain the common values.
- `isDpiScaleUniform` is `true`.
- All-screens output includes `monitorSegments`; a single-monitor region continues using its existing monitor fields.

### Mixed-DPI capture

- `dpi` and `scale` are `null`.
- `isDpiScaleUniform` is `false`.
- `monitorSegments` contains one entry for every monitor intersecting the captured source bounds.

Each segment includes:

- `monitorId`: process-local `hmonitor:<value>` identifier;
- `sourceBounds`: the contributing rectangle in virtual-screen coordinates;
- `imageBounds`: the same pixels translated into returned-image coordinates;
- `dpi` and `scale`: values for that monitor;
- `isPrimary`: whether the monitor is primary.

## Coordinate requirements

1. Segment source bounds are clipped to the capture source rectangle.
2. Segment image bounds use the capture source rectangle's top-left as image origin.
3. Negative virtual-screen coordinates translate to non-negative image coordinates.
4. Monitor ordering follows the capture service's monitor enumeration order.
5. Desktop gaps are not represented as monitor segments.

## Reliability and compatibility

- Empty monitor sets continue to fail explicitly.
- A requested region that intersects no monitor continues to fail explicitly.
- Uniform composites no longer depend on which monitor is primary; their common DPI is used.
- Existing clients reading scalar values continue to receive them for uniform output.
- Clients must use the segment map when `isDpiScaleUniform` is false.

## Test plan

Add tests for:

- uniform all-screens capture preserving scalar DPI/scale and publishing all segments;
- mixed-DPI all-screens capture nulling scalars and translating negative monitor coordinates;
- mixed-DPI spanning region clipping source segments and mapping them into image coordinates;
- annotated mixed-DPI captures preserving the source segment map.

Run all non-UI test projects and build the WinUI x64 Debug project.

## Acceptance criteria

- [x] Mixed-DPI composite captures do not publish a single authoritative DPI or scale.
- [x] Uniform composite captures retain scalar DPI and scale.
- [x] Per-monitor segments map source and image bounds with monitor DPI and scale.
- [x] Annotated derivatives preserve mixed-DPI metadata.
- [x] Regression coverage exercises mixed-DPI all-screens and spanning-region paths.
- [x] Existing non-UI tests pass and the WinUI x64 Debug project builds.

## Rollout

No migration or feature flag is required. The structured metadata change is additive except that mixed-DPI composite scalar values become nullable instead of misleading reference-monitor values.
