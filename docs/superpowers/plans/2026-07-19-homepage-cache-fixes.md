# HomePageCache Quality Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix key registration race, mutable list sharing, and scan refresh cache bypass issues in HomePageCache and PhotoGrid.

**Architecture:** Update HomePageCache to register keys inside the memory cache factory callback and return copies of lists, and update PhotoGrid to bypass cache during scan refreshes.

**Tech Stack:** C#, .NET 10, Blazor, xUnit

---

### Task 1: Fix Key Registration Race in HomePageCache

**Files:**
- Modify: `src/KelliPhoto.Web/Services/HomePageCache.cs`

- [ ] **Step 1: Register key inside factory callback**
  Move `_keys.TryAdd(key, 0)` inside the `GetOrCreateAsync` callback before calling `await factory()`.
  Also register `entry.RegisterPostEvictionCallback` to remove the key from `_keys` when evicted.

- [ ] **Step 2: Clean up old key registration**
  Remove the `if (isMiss)` block after `GetOrCreateAsync` that adds the key to `_keys`.

- [ ] **Step 3: Verify compilation**
  Run `dotnet build KelliPhoto.sln` to ensure it builds successfully.

---

### Task 2: Don't Share Mutable Cached Lists in GetFirstPagePhotosAsync

**Files:**
- Modify: `src/KelliPhoto.Web/Services/HomePageCache.cs`

- [ ] **Step 1: Store a copy of the factory result in the cache**
  In `GetFirstPagePhotosAsync`, inside the `GetOrCreateAsync` factory callback, call `.ToList()` on the result of `await factory()` before returning it.

- [ ] **Step 2: Return a new copy to callers**
  Return a new `.ToList()` copy of the cached list (or a new empty list if null) to callers.

- [ ] **Step 3: Verify compilation**
  Run `dotnet build KelliPhoto.sln` to ensure it builds successfully.

---

### Task 3: Update HomePageCacheTests to Match New Copying Behavior

**Files:**
- Modify: `tests/KelliPhoto.Web.Tests/HomePageCacheTests.cs`

- [ ] **Step 1: Update GetFirstPagePhotosAsync_SecondCallUsesCache test**
  Change `Assert.Same(photos, result1)` to `Assert.NotSame(photos, result1)` and `Assert.Equal(photos, result1)`.
  Change `Assert.Same(photos, result2)` to `Assert.NotSame(result1, result2)` and `Assert.Equal(photos, result2)`.

- [ ] **Step 2: Update GetFirstPagePhotosAsync_DifferentIncludeHiddenKeysAreSeparate test**
  Update all assertions on list references from `Assert.Same` to `Assert.NotSame` and `Assert.Equal`.

- [ ] **Step 3: Run the tests**
  Run `dotnet test KelliPhoto.sln --filter HomePageCache` to verify they pass.

---

### Task 4: Bypass Cache on Scan Refresh in PhotoGrid

**Files:**
- Modify: `src/KelliPhoto.Web/Components/PhotoGrid.razor`

- [ ] **Step 1: Update RefreshPhotosAsync**
  In `RefreshPhotosAsync`, do NOT use `HomePageCache` — always hit `PhotoService` directly.

- [ ] **Step 2: Verify compilation and run full test suite**
  Run `dotnet test KelliPhoto.sln` to ensure everything passes.
