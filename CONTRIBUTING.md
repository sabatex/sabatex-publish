# Contributing to sabatex-publish

## Version Management Rules for `.csproj` Files

### Overview
`sabatex-publish` automatically detects project version and publishes to different targets based on version format.

---

## 📦 Publishing Destinations

### **Release Versions** (`major.minor.patch`)
✅ Published to **NuGet.org** (public repository)

**Examples:**
- `1.0.0` ✅ → NuGet.org
- `2.5.3` ✅ → NuGet.org
- `10.0.1` ✅ → NuGet.org

**Command:**
````````

---

### **Pre-Release Versions** (with suffix)
✅ Published to **Local NuGet Repository** (development/testing)

**Examples:**
- `1.0.0-rc1` ✅ → Local storage
- `2.5.3-beta` ✅ → Local storage
- `10.0.1-alpha` ✅ → Local storage

**Command:**
````````

---

## 🔢 Version Format Rules

### 1. **Direct Version (Standard)**
For standalone projects that need independent publishing:

✅ **Used for:** Libraries, executables published independently.

---

### 2. **Inherited Version (Referenced Project)**
For helper/companion projects that share version with main library:

✅ **Used for:** Server-side companions, platform-specific extensions.  
📌 **Behavior:** `sabatex-publish` reads version from first `ProjectReference` with non-empty `<Version>`.

**Example:**
````````
<ProjectReference Include="..\MyLib\MyLib.csproj">
  <Version>1.2.3</Version>
</ProjectReference>
````````

✅ **Result:** Server library published to **Local storage** (pre-release detected).

---

### 3. **No Version (Non-publishable)**
For build helpers, test projects, or infrastructure:

⏭️ **Behavior:** Project **skipped** during batch publishing with info message.

---

## 🔄 Version Resolution Algorithm

`sabatex-publish` follows this priority:

1. **Direct `<Version>` in current project**
   - ✅ If found → use it
   
2. **If empty/missing** → search **ALL** `<ProjectReference>` elements
   - 🔍 Read version from all referenced projects
   - ✅ **0 versions found** → ❌ Error (exit code 6)
   - ✅ **1 version found** → Use it
   - ✅ **Multiple versions found** → ❌ Error (exit code 11 - ambiguous)

3. **Determine publishing target**
   - 🔍 Check version format
   - ✅ **Release** (`1.0.0`) → NuGet.org
   - ✅ **Pre-release** (`1.0.0-rc1`) → Local storage

---

## 📋 Example Scenarios

### Scenario 1: WASM + Server Library Pattern

**Main Library (WASM):**
````````
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework>
    <Version>1.0.0</Version>
  </PropertyGroup>

</Project>
````````
📦 **Published to:** NuGet.org

---

**Server Extension:**
````````
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework>
    <Version></Version> <!-- Inherits from main library -->
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\MainLibrary\MainLibrary.csproj" />
  </ItemGroup>

</Project>
`````````
📦 **Published to:** Local storage (pre-release)

---

### Scenario 2: Plugin Architecture

**Plugin Base Library:**
````````
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework>
    <Version>2.5.0</Version>
  </PropertyGroup>

</Project>
`````````
📦 **Published to:** NuGet.org

---

**Plugin A:**
````````
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework>
    <Version>2.5.0-alpha</Version> <!-- Pre-release version -->
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\PluginBase\PluginBase.csproj" />
  </ItemGroup>

</Project>
`````````
📦 **Published to:** Local storage

---

**Plugin B:**
````````
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework>
    <Version></Version> <!-- Inherits version from PluginBase -->
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\PluginBase\PluginBase.csproj" />
  </ItemGroup>

</Project>
`````````
📦 **Published to:** Local storage (pre-release)

---

## 🧪 Testing Your Changes

### Local Publishing Setup
1. Ensure [NuGet.Config](https://docs.microsoft.com/en-us/nuget/reference/nuget-config-file) is set up to include local repository path.
2. Use `dotnet pack` to create NuGet package locally.
3. Install the package from the local repository to test integration.

### Common Commands
- Pack: `dotnet pack path/to/your.csproj`
- Push to local: `dotnet nuget push yourpackage.nupkg --source "Local NuGet Gallery"`
- Restore from local: `dotnet restore yoursolution.sln`

---

## FAQ

**Q: Why is my project not publishing?**  
A: Check if `<Version>` is set correctly. Empty or missing version will skip the project.

**Q: How to publish a pre-release version?**  
A: Append a suffix to the version (e.g., `1.0.0-alpha`). Ensure local publishing is set up.

**Q: Can I publish directly to NuGet.org?**  
A: Yes, set a valid release version (`major.minor.patch`) and use the publish command.

---

## Additional Resources
- [Official NuGet Documentation](https://docs.microsoft.com/en-us/nuget/)

📦 **Published to:** NuGet.org (public)

---

## ✅ Best Practices

### DO:
- ✅ Use semantic versioning (`major.minor.patch`)
- ✅ Use `-rc`, `-beta`, `-alpha` suffixes for testing
- ✅ Keep versions synchronized for related projects
- ✅ Document version inheritance in comments
- ✅ Test with pre-release versions before publishing to NuGet.org

### DON'T:
- ❌ Leave `<Version>` empty without `ProjectReference`
- ❌ Mix different versioning schemes in one solution
- ❌ Publish untested code to NuGet.org
- ❌ Forget to update version before release

---

## 🛠️ Troubleshooting

### Error: "The version must have kind *.*.*"
**Cause:** Empty or invalid `<Version>` element.  

**Fix:**
1. Add valid version: `<Version>1.0.0</Version>`, OR
2. Add `<ProjectReference>` to project with version, OR
3. Disable project in `sabatex-publish-solution.json`

---

### Project skipped during batch publishing
**Cause:** No `<Version>` element found.

**Fix:**
- Add `<Version>1.0.0</Version>`, OR
- Add `<ProjectReference>` to inherit version, OR
- Mark as `"enabled": false` in batch config

---

### Published to wrong target (NuGet.org instead of local)
**Cause:** Version has no suffix.

**Fix:** Add pre-release suffix: