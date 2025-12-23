# Sabatex Publish - GitHub Copilot Instructions

## Project Overview
**sabatex-publish** is a .NET CLI tool for automated publishing of .NET projects to:
- **NuGet** (for libraries)
- **Linux servers** (for web applications via SSH)

**Technology Stack:**
- C# 13.0
- .NET 9
- System.CommandLine for CLI
- Renci.SSH.NET for Linux deployment

---

## Architecture

### Core Components

1. **Program.cs** - Entry point with unified publishing loop
   - Single `Main()` method handles all modes
   - Unified loop processes projects regardless of mode (single/batch)

2. **CommandProcessor.cs** - CLI argument parsing
   - Returns: `(exitCode, shouldExit, projFile, solutionFolder, migrate, updateService, updateNginx)`
   - No mode detection logic - only parameter parsing

3. **SabatexSettings.cs** - Project configuration
   - Loaded per-project
   - Contains Linux, NUGET, build settings
   - `ResolveConfig()` initializes all properties

4. **BatchPublisher.cs** - Batch config loading only
   - `LoadConfig()` - reads `sabatex-publish-solution.json`
   - No publishing logic (moved to `Main()`)

---

## Publishing Modes

### 1. Single Project Mode

### 2. Batch Mode

### Configuration Priority
1. **Explicit parameters** (`--csproj`, `--solution`) - highest priority
2. **Batch config** (`sabatex-publish-solution.json`) - if exists in current directory
3. **Auto-detect single .csproj** - fallback
4. **Error** - if multiple .csproj or nothing found

---

## Code Style Rules

### 1. Parameter Passing
**ALWAYS pass `SabatexSettings settings` as parameter to methods:**

### 2. Verbose Logging
**Enable verbose logging with `--verbose` flag in CLI.**

### 3. Logging
**Use `Logger` class instead of `Console.WriteLine`:**

### 4. Error Handling
**Use exceptions for errors, return exit codes:**

---

## Main() Publishing Loop

**Unified loop for all modes:**

---

## Configuration Files

### Project-level: `appsettings.json`

### Solution-level: `sabatex-publish-solution.json`

---

## Common Patterns

### Create Linux Script Shells

### Build & Publish

---

## Important Notes

### ⚠️ Don't Add These Comments
**Avoid technical change comments:**

### ⚠️ DateTime Formatting
**Don't change existing datetime formatting:**

---

## Exit Codes

- `0` - Success
- `1` - Parsing errors or publishing failures
- `2-9` - Configuration errors (from `SabatexSettings.ResolveConfig()`)

---

## File Structure

---

## When Making Changes

1. **Always pass `settings` as parameter** - no global state
2. **Use `Logger` for all output** - consistent logging
3. **Check `settings.Linux` for null** - avoid NullReferenceException
4. **Keep Main() unified loop** - don't split single/batch logic
5. **PRESERVE ALL EXISTING COMMENTS** - only add comments with `//#` prefix
6. **Follow existing patterns** - consistency is key
7. **Make ONLY requested changes** - don't modify unrelated code
8. **When problem/question: discuss FIRST, code AFTER** - avoid premature solutions

---

## Comment Convention Summary

| Prefix | Owner | Action |
|--------|-------|--------|
| `//` | User | **PRESERVE** - Never delete or modify |
| `//#` | Copilot | **ADD** - When explanation needed |

---

*Last updated: 2025-12-23*

---

## Response Strategy

### 🔴 When User Asks Question or Describes Problem

**FIRST: Discuss options WITHOUT code**

### 🟢 When User Gives Direct Instruction

**IMMEDIATELY: Provide code**