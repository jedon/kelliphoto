# Watermark Debugging Plan

## Overview
This document outlines a comprehensive plan to debug watermark functionality using unit tests and systematic investigation.

## Current Issues
- Watermarks are not being applied to images
- Path resolution may be failing (UNC paths, relative paths)
- Configuration may not be loading correctly
- File existence checks may be failing

## Test Coverage Plan

### 1. Configuration Tests ✅
**File**: `WatermarkServiceTests.cs`

Tests to verify:
- ✅ `GetWatermarkSettings_WhenEnabledAndPathExists_ShouldReturnEnabled`
- ✅ `GetWatermarkSettings_WhenEnabledButPathDoesNotExist_ShouldReturnEnabledButNoPath`
- ✅ `GetWatermarkSettings_WhenPathNotSet_ShouldFallbackToWebAssetsPath`
- ✅ `GetWatermarkSettings_WhenUncPathProvided_ShouldHandleUncPath`
- ✅ `GetWatermarkSettings_WhenWatermarkNotRequested_ShouldReturnDisabled`
- ✅ `GetWatermarkSettings_WhenEnabledIsFalse_ShouldReturnDisabled`

**What these test:**
- Configuration reading from appsettings
- Path resolution logic
- Fallback to WebAssetsPath
- UNC path handling
- Enabled/disabled state logic

### 2. Integration Tests ✅
**File**: `WatermarkServiceTests.cs`

Tests to verify:
- ✅ `CreateWebImageAsync_WhenWatermarkEnabledAndPathExists_ShouldApplyWatermark`
- ✅ `CreateWebImageAsync_WhenWatermarkEnabledButPathDoesNotExist_ShouldNotApplyWatermark`

**What these test:**
- End-to-end watermark application
- Image generation with watermark
- Error handling when watermark file doesn't exist

### 3. Cache Key Tests ✅
**File**: `WatermarkServiceTests.cs`

Tests to verify:
- ✅ `BuildCacheKey_WhenWatermarkEnabled_ShouldIncludeWatermarkParameters`
- ✅ `BuildCacheKey_WhenWatermarkDisabled_ShouldNotIncludeWatermarkParameters`

**What these test:**
- Cache key generation includes watermark parameters
- Different cache keys for watermarked vs non-watermarked images

## Debugging Steps

### Step 1: Run Unit Tests
```bash
dotnet test tests/KelliPhoto.Web.Tests/KelliPhoto.Web.Tests.csproj --filter "FullyQualifiedName~WatermarkServiceTests"
```

**Expected Results:**
- All tests should pass if watermark logic is correct
- If tests fail, they will pinpoint the exact issue

### Step 2: Check Configuration Loading
**Test**: Verify configuration is loaded correctly

```csharp
// Add to Program.cs or create a diagnostic endpoint
var enabled = configuration.GetValue<bool>("WatermarkSettings:Enabled");
var imagePath = configuration["WatermarkSettings:ImagePath"];
var webAssetsPath = configuration["GallerySettings:WebAssetsPath"];
logger.LogInformation("Watermark Config - Enabled: {Enabled}, ImagePath: {ImagePath}, WebAssetsPath: {WebAssetsPath}", 
    enabled, imagePath, webAssetsPath);
```

### Step 3: Verify File Existence
**Test**: Check if watermark file exists at expected paths

```csharp
// Test paths in order of priority:
// 1. WatermarkSettings:ImagePath
// 2. GallerySettings:WebAssetsPath/watermark.png
// 3. Check UNC path accessibility
```

### Step 4: Test Path Resolution
**Test**: Verify UNC path handling

```csharp
var paths = new[]
{
    @"\\darklingnas\Kelli\kelli.photo\.web\watermark.png",
    @"\\server\share\watermark.png",
    @"C:\path\to\watermark.png",
    @".\watermark.png"
};

foreach (var path in paths)
{
    var exists = File.Exists(path);
    var isUnc = path.StartsWith(@"\\");
    logger.LogInformation("Path: {Path}, Exists: {Exists}, IsUNC: {IsUNC}", path, exists, isUnc);
}
```

### Step 5: Test Watermark Application
**Test**: Create a simple test image and verify watermark is applied

```csharp
// Create test image
// Apply watermark
// Verify watermark appears in output
// Check image dimensions and watermark position
```

## Common Issues and Solutions

### Issue 1: Path Not Found
**Symptoms**: `Watermark enabled but WatermarkSettings:ImagePath missing or not found`
**Solutions**:
- Verify file exists at configured path
- Check UNC path accessibility (network share permissions)
- Verify WebAssetsPath fallback is working
- Check path normalization (forward vs backslashes)

### Issue 2: Configuration Not Loading
**Symptoms**: Watermark settings always disabled or null
**Solutions**:
- Verify appsettings.json is being loaded
- Check environment-specific settings (appsettings.Development.json)
- Verify configuration key names match exactly
- Check for typos in configuration keys

### Issue 3: UNC Path Issues
**Symptoms**: File.Exists returns false for UNC paths
**Solutions**:
- Verify network share is accessible
- Check Windows credentials/permissions
- Try accessing path directly in File Explorer
- Consider using PathService for path normalization

### Issue 4: Watermark Not Applied
**Symptoms**: Images generated but no watermark visible
**Solutions**:
- Check if watermark.Enabled is true
- Verify watermark.ImagePath is not null/empty
- Check if File.Exists(watermark.ImagePath) returns true
- Verify ApplyImageWatermarkAsync is being called
- Check watermark opacity (may be too low to see)
- Verify watermark position (may be outside visible area)

## Diagnostic Endpoints (Optional)

Add these to ImagesController for runtime debugging:

```csharp
[HttpGet("api/debug/watermark")]
public IActionResult GetWatermarkDebugInfo()
{
    var config = _configuration;
    var enabled = config.GetValue<bool>("WatermarkSettings:Enabled");
    var imagePath = config["WatermarkSettings:ImagePath"];
    var webAssetsPath = config["GallerySettings:WebAssetsPath"];
    var pathExists = !string.IsNullOrEmpty(imagePath) && File.Exists(imagePath);
    
    var fallbackPath = !string.IsNullOrEmpty(webAssetsPath) 
        ? Path.Combine(webAssetsPath, "watermark.png") 
        : null;
    var fallbackExists = !string.IsNullOrEmpty(fallbackPath) && File.Exists(fallbackPath);
    
    return Ok(new
    {
        Enabled = enabled,
        ImagePath = imagePath,
        ImagePathExists = pathExists,
        WebAssetsPath = webAssetsPath,
        FallbackPath = fallbackPath,
        FallbackPathExists = fallbackExists
    });
}
```

## Next Steps

1. ✅ Create unit tests (WatermarkServiceTests.cs)
2. Run tests to identify failures
3. Fix issues identified by tests
4. Add integration tests with real images
5. Test with actual UNC paths in development environment
6. Verify watermark appears in generated images
7. Test cache key generation for watermarked images

## Running the Tests

```bash
# Run all watermark tests
dotnet test tests/KelliPhoto.Web.Tests/KelliPhoto.Web.Tests.csproj --filter "FullyQualifiedName~WatermarkServiceTests"

# Run specific test
dotnet test tests/KelliPhoto.Web.Tests/KelliPhoto.Web.Tests.csproj --filter "FullyQualifiedName~WatermarkServiceTests.GetWatermarkSettings_WhenEnabledAndPathExists_ShouldReturnEnabled"

# Run with verbose output
dotnet test tests/KelliPhoto.Web.Tests/KelliPhoto.Web.Tests.csproj --filter "FullyQualifiedName~WatermarkServiceTests" --verbosity detailed
```

## Expected Test Results

When all tests pass:
- ✅ Configuration is loading correctly
- ✅ Path resolution is working
- ✅ Watermark application logic is correct
- ✅ Cache keys include watermark parameters

If tests fail:
- Review failure messages to identify specific issues
- Check test output for detailed error information
- Fix issues in WebImageService based on test failures
- Re-run tests to verify fixes
