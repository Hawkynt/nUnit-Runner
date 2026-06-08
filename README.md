# nUnit-Runner

[![License](https://img.shields.io/github/license/Hawkynt/nUnit-Runner)](https://github.com/Hawkynt/nUnit-Runner/blob/main/LICENSE)
[![Language](https://img.shields.io/github/languages/top/Hawkynt/nUnit-Runner?color=8957D5)](https://github.com/Hawkynt/nUnit-Runner)

[![CI](https://github.com/Hawkynt/nUnit-Runner/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/Hawkynt/nUnit-Runner/actions/workflows/ci.yml)
![Last Commit](https://img.shields.io/github/last-commit/Hawkynt/nUnit-Runner?branch=main)
![Activity](https://img.shields.io/github/commit-activity/m/Hawkynt/nUnit-Runner)

[![Stars](https://img.shields.io/github/stars/Hawkynt/nUnit-Runner?color=FFD700)](https://github.com/Hawkynt/nUnit-Runner/stargazers)
[![Forks](https://img.shields.io/github/forks/Hawkynt/nUnit-Runner?color=008080)](https://github.com/Hawkynt/nUnit-Runner/network/members)
[![Issues](https://img.shields.io/github/issues/Hawkynt/nUnit-Runner)](https://github.com/Hawkynt/nUnit-Runner/issues)
![Code Size](https://img.shields.io/github/languages/code-size/Hawkynt/nUnit-Runner?color=4CAF50)
![Repo Size](https://img.shields.io/github/repo-size/Hawkynt/nUnit-Runner?color=FF9800)

[![Release](https://img.shields.io/github/v/release/Hawkynt/nUnit-Runner)](https://github.com/Hawkynt/nUnit-Runner/releases/latest)
[![Nightly](https://img.shields.io/github/v/release/Hawkynt/nUnit-Runner?include_prereleases&sort=date&label=nightly&color=FF9800)](https://github.com/Hawkynt/nUnit-Runner/releases)
[![Downloads](https://img.shields.io/github/downloads/Hawkynt/nUnit-Runner/total)](https://github.com/Hawkynt/nUnit-Runner/releases)

> A universal NUnit test runner for .NET Framework 2.0 through .NET 9.0+

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
| -------------------------------------------- | ------------- | ---------------- |
| net20, net35, net40, net45                   | netcoreapp3.1 | net5.0 - net10.0 |
| net461, net462, net47, net471, net472, net48 |               |                  |

## 🚀 Usage

```bash
# Run all framework variants
TestRunner Tests/bin/Release --all

# Run in parallel
TestRunner Tests/bin/Release --all --parallel

# Specific frameworks only
TestRunner Tests/bin/Release --all --frameworks net20,net48,net9.0

# Single assembly
TestRunner Tests/bin/Release/net48/MyTests.dll

# Filter by test name (substring match)
TestRunner Tests/bin/Release --all --filter StringTests

# NUnit-style where expressions
TestRunner Tests.dll --where "cat == Unit"
TestRunner Tests.dll --where "class =~ /String.*/"
TestRunner Tests.dll --where "cat == Unit and not method == SlowTest"
TestRunner Tests.dll --where "(cat == Integration or cat == E2E) and namespace =~ /MyApp.*/"
```

### Where Expression Syntax

The `--where` option supports NUnit-style filter expressions:

| Property           | Description                                 |
| ------------------ | ------------------------------------------- |
| `cat` / `category` | Test category (from `[Category]` attribute) |
| `class`            | Test fixture class name                     |
| `method`           | Test method name                            |
| `namespace`        | Namespace of the test class                 |
| `test` / `name`    | Full test name (Namespace.Class.Method)     |

| Operator | Description               |
| -------- | ------------------------- |
| `!~`     | Regex does not match      |
| `!=`     | Not equals                |
| `=~`     | Regex match               |
| `==`     | Equals (case-insensitive) |

| Boolean | Description                   |
| ------- | ----------------------------- |
| `()`    | Grouping for precedence       |
| `and`   | Both conditions must be true  |
| `not`   | Inverts the condition         |
| `or`    | Either condition must be true |

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

## 🛠️ Build

```bash
dotnet build -c Release
```

## 📦 Dependencies

For net20-net45, requires [FrameworkExtensions.Backports](https://www.nuget.org/packages/FrameworkExtensions.Backports) for Task/async support.

## ❤️ Support

If this project saves you time or money, consider supporting its development:

[![GitHub Sponsors](https://img.shields.io/badge/GitHub-Sponsor-EA4AAA?logo=githubsponsors)](https://github.com/sponsors/Hawkynt)
[![PayPal](https://img.shields.io/badge/PayPal-Donate-00457C?logo=paypal)](https://www.paypal.me/hawkynt)

## 📜 License

Licensed under LGPL-3.0-or-later — see [LICENSE](LICENSE).
