# Phase 4 Build Fix Plan

## Overview
This document outlines the strategy to investigate and resolve build errors in the CaptureTool project after integrating Phase 4 encoder/muxer architecture.

## Current Status
- **Last Commit**: 11e4bc7 - "Fix CaptureInterop.vcxproj build errors: correct ClInclude/ClCompile for .cpp files"
- **Issue**: Build errors reported in CI pipeline
- **Affected Projects**: CaptureInterop, CaptureInterop.Tests

## Investigation Strategy

### Step 1: Identify Build Errors
Since we cannot access CI logs directly, we need to methodically check for potential issues:

1. **Project File Consistency**
   - Verify all `.cpp` files are in `<ClCompile>` sections
   - Verify all `.h` files are in `<ClInclude>` sections
   - Check for duplicate entries
   - Ensure all physical files have corresponding project entries

2. **Missing Files**
   - Check if all referenced files exist in the file system
   - Verify project references are correct

3. **Compilation Issues**
   - Missing includes
   - Circular dependencies
   - Syntax errors in new code
   - Missing dependencies (Media Foundation, D3D11, etc.)

### Step 2: Verify Project Structure

**Files to Check:**
- `src/CaptureInterop/CaptureInterop.vcxproj`
- `src/CaptureInterop/CaptureInterop.vcxproj.filters`
- `src/CaptureInterop.Tests/CaptureInterop.Tests.vcxproj`
- `src/CaptureInterop.Tests/CaptureInterop.Tests.vcxproj.filters`

**Verification Points:**
1. All Phase 4 files properly listed:
   - EncoderPresets.h/cpp ✓
   - IVideoEncoder.h ✓
   - IAudioEncoder.h ✓
   - IMuxer.h ✓
   - H264VideoEncoder.h/cpp ✓
   - AACEncoder.h/cpp ✓
   - MP4Muxer.h/cpp ✓
   - EncoderPipeline.h/cpp ✓
   - TextureConverter.h/cpp ✓

2. No files listed in wrong sections (ClInclude vs ClCompile)

3. All files exist on disk

### Step 3: Common Build Issues to Check

#### Issue 1: Missing Dependencies
**Symptoms**: Linker errors, unresolved external symbols
**Files Affected**: H264VideoEncoder.cpp, AACEncoder.cpp, MP4Muxer.cpp
**Solution**: Verify Media Foundation and D3D11 libraries are linked
```xml
<AdditionalDependencies>
  mfplat.lib;
  mf.lib;
  mfreadwrite.lib;
  mfuuid.lib;
  d3d11.lib;
  dxgi.lib;
  %(AdditionalDependencies)
</AdditionalDependencies>
```

#### Issue 2: Missing Includes
**Symptoms**: C2065 undeclared identifier, C2039 is not a member
**Files Affected**: Any new Phase 4 files
**Solution**: 
- Check #include statements in .cpp files
- Verify pch.h includes necessary headers
- Check for missing Windows headers (mfapi.h, d3d11.h, etc.)

#### Issue 3: Forward Declaration Issues
**Symptoms**: C2027 use of undefined type
**Files Affected**: Interface files (IVideoEncoder.h, IAudioEncoder.h, IMuxer.h)
**Solution**:
- Add proper forward declarations
- Include necessary headers in the right order

#### Issue 4: Precompiled Header Issues
**Symptoms**: C1010 unexpected end of file while looking for precompiled header
**Files Affected**: Any .cpp file
**Solution**:
- Ensure all .cpp files include "pch.h" as first include
- Verify pch.cpp has PrecompiledHeader set to "Create"
- Verify other .cpp files have PrecompiledHeader set to "Use"

#### Issue 5: Circular Dependencies
**Symptoms**: C2504 base class undefined
**Files Affected**: Classes with complex inheritance
**Solution**:
- Review header include order
- Use forward declarations where possible
- Break circular dependencies with interfaces

#### Issue 6: Windows SDK Version
**Symptoms**: Cannot open include file windows.h
**Solution**:
- Verify WindowsTargetPlatformVersion in project files
- Current version: 10.0.26100.0
- May need to be adjusted for CI environment

### Step 4: Specific Areas to Investigate

#### A. EncoderPipeline Integration
**Potential Issues:**
- Missing includes for H264VideoEncoder, AACEncoder, MP4Muxer
- Incorrect interface usage
- COM reference counting issues

**Files to Review:**
- `EncoderPipeline.h` - Check includes and forward declarations
- `EncoderPipeline.cpp` - Check implementation

#### B. TextureConverter
**Potential Issues:**
- D3D11 Video Processor API usage
- Media Foundation sample creation
- Missing DirectX dependencies

**Files to Review:**
- `TextureConverter.h`
- `TextureConverter.cpp`

#### C. H264VideoEncoder
**Potential Issues:**
- Media Foundation Transform (MFT) API usage
- Hardware encoder detection
- ICodecAPI usage

**Files to Review:**
- `H264VideoEncoder.h`
- `H264VideoEncoder.cpp`

#### D. AACEncoder
**Potential Issues:**
- AAC frame buffering logic
- Media Foundation encoder configuration
- Sample alignment

**Files to Review:**
- `AACEncoder.h`
- `AACEncoder.cpp`

#### E. MP4Muxer
**Potential Issues:**
- IMFSinkWriter usage
- Multi-track stream management
- Interleaving algorithm

**Files to Review:**
- `MP4Muxer.h`
- `MP4Muxer.cpp`

### Step 5: Test Project Issues

**Potential Problems:**
1. Missing CaptureInterop project reference
2. Incorrect platform toolset (v143 may not match CaptureInterop)
3. Missing test framework libraries
4. Linker errors due to missing exports

**Checks:**
- Verify ProjectReference in CaptureInterop.Tests.vcxproj
- Ensure PlatformToolset matches between projects
- Check for proper test framework setup

### Step 6: Resolution Steps

**Priority Order:**
1. **High Priority**: Fix any missing .cpp files in ClCompile sections
2. **High Priority**: Add missing library dependencies
3. **Medium Priority**: Fix missing includes and forward declarations
4. **Medium Priority**: Resolve precompiled header issues
5. **Low Priority**: Address warnings and code style issues

### Step 7: Validation

After applying fixes:
1. Verify all files compile individually
2. Check linker can resolve all symbols
3. Ensure test project builds
4. Run unit tests (once build succeeds)

## Next Actions

1. Manually review all project files for inconsistencies
2. Check each new Phase 4 source file for missing includes
3. Verify library dependencies are properly configured
4. Create targeted fixes for each identified issue
5. Test build locally (if possible) or push fixes incrementally

## Expected Issues (Based on Analysis)

Given the complexity of Phase 4 integration, the most likely issues are:

1. **Missing Media Foundation libraries** in linker configuration
2. **Missing includes** in new encoder files
3. **Precompiled header** issues in new files
4. **Project reference** issues between CaptureInterop and test project

## Resolution Timeline

- **Investigation**: Review project files and code (30 min)
- **Fix Application**: Apply targeted fixes (1-2 hours)
- **Validation**: Push and verify CI build (multiple iterations)
- **Total Estimated Time**: 2-4 hours

## Success Criteria

- All C++ projects build without errors
- All tests compile (tests don't need to pass yet)
- No linker errors
- No missing symbol errors
- Clean CI build status
