# 🧪 nUnit-Runner

[![License](https://img.shields.io/badge/License-LGPL_3.0-blue)](https://licenses.nuget.org/LGPL-3.0-or-later)
![Language](https://img.shields.io/github/languages/top/Hawkynt/nUnit-Runner?color=purple)
[![Last Commit](https://img.shields.io/github/last-commit/Hawkynt/nUnit-Runner?branch=main)](https://github.com/Hawkynt/nUnit-Runner/commits/main)
[![Stars](https://img.shields.io/github/stars/Hawkynt/nUnit-Runner?color=yellow)](https://github.com/Hawkynt/nUnit-Runner/stargazers)

> 🚀 A universal NUnit test runner for .NET Framework 2.0 through .NET 9.0+

## ❓ Why?

When testing polyfill libraries or multi-targeted assemblies, each target framework **must** run on its native CLR. Running a net20 test assembly on net48 would use the real BCL types instead of polyfills, giving false results.

✅ This runner ensures each framework's tests execute on the correct runtime.

## ⚙️ How It Works

The project builds to **multiple separate executables**, one per target framework:

```
📁 bin/Release/
  📂 net20/TestRunner.exe       ← Runs on .NET Framework 2.0 CLR
  📂 net35/TestRunner.exe       ← Runs on .NET Framework 3.5 CLR
  📂 net48/TestRunner.exe       ← Runs on .NET Framework 4.8 CLR
  📂 net9.0/TestRunner.dll      ← Runs on .NET 9 runtime
  ...
```

When you run any version with `--all`, it acts as **orchestrator**:

1. 🔍 Discovers test assemblies in framework subfolders
2. 🏃 Spawns the matching `TestRunner.exe` from sibling folders
3. 📤 Each worker runs tests and outputs JSON
4. 📊 Orchestrator aggregates and displays results

```
You run: bin/Release/net9.0/TestRunner.dll Tests/bin/Release --all
         │
         ├─ Spawns: ../net20/TestRunner.exe  → Tests net20 assembly
         ├─ Spawns: ../net35/TestRunner.exe  → Tests net35 assembly
         ├─ Spawns: ../net48/TestRunner.exe  → Tests net48 assembly
         └─ Runs directly                    → Tests net9.0 assembly
         │
         └─ Aggregates all results
```

## 🎯 Supported Frameworks

| .NET Framework                               | .NET Core     | .NET             |
|----------------------------------------------|---------------|------------------|
| net20, net35, net40, net45                   | netcoreapp3.1 | net5.0 - net10.0 |
| net461, net462, net47, net471, net472, net48 |               |                  |

## 📋 Usage

```bash
# Run all framework variants
TestRunner Tests/bin/Release --all

# Run in parallel
TestRunner Tests/bin/Release --all --parallel

# Specific frameworks only
TestRunner Tests/bin/Release --all --frameworks net20,net48,net9.0

# Single assembly
TestRunner Tests/bin/Release/net48/MyTests.dll

# Filter by test name
TestRunner Tests/bin/Release --all --filter StringTests
```

## 📊 Example Output

```
Universal Test Runner v1.0
======================================================================

Found 4 test assembly(ies):
  [net20] Backports.Tests.dll
  [net35] Backports.Tests.dll
  [net48] Backports.Tests.dll
  [net9.0] Backports.Tests.dll

Running [net20]...
  ✅ Passed: 3296, ❌ Failed: 2, ⏭️ Skipped: 1

Running [net35]...
  ✅ Passed: 3298, ❌ Failed: 0, ⏭️ Skipped: 1

Running [net48]...
  ✅ Passed: 3298, ❌ Failed: 0, ⏭️ Skipped: 1

Running [net9.0]...
  ✅ Passed: 3300, ❌ Failed: 0, ⏭️ Skipped: 0

======================================================================
Test Run Summary
======================================================================

Framework        Passed   Failed  Skipped Status
----------------------------------------------------------------------
net20              3296        2        1 ❌ FAIL
net35              3298        0        1 ✅ PASS
net48              3298        0        1 ✅ PASS
net9.0             3300        0        0 ✅ PASS
----------------------------------------------------------------------
TOTAL             13192        2        3

⏱️ Duration: 42.35s

Frameworks with failures:
  [net20]
    - StringTests.Format_WithSpan(buffer: "ABC..XYZ..123" [2000], format: "{0:X8}", value: 255)
      Expected: "000000FF" But was: "ff"
    - StringTests.Create_WithState(length: 10, state: 42)
      Object reference not set to an instance of an object
```

## 🔨 Build

```bash
dotnet build -c Release
```

## 📦 Dependencies

For net20-net45, requires [FrameworkExtensions.Backports](https://www.nuget.org/packages/FrameworkExtensions.Backports) for Task/async support.

## 📄 Licensing

* [LGPL-3.0](https://en.wikipedia.org/wiki/GNU_Lesser_General_Public_License)
* 🐛 Report issues at [GitHub](https://github.com/Hawkynt/nUnit-Runner)
