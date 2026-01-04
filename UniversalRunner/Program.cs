using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace UniversalTestRunner;

internal class Program {
  private static int Main(string[] args) {
    var options = ParseArgs(args);

    if (options.ShowHelp || (options.AssemblyPaths.Count == 0 && options.TestDirectory == null)) {
      ShowHelp();
      return options.ShowHelp ? 0 : 1;
    }

    // Worker mode: run tests and output JSON
    if (options.WorkerMode) {
      return RunAsWorker(options);
    }

    // Orchestrator mode: spawn workers and aggregate results
    return RunAsOrchestrator(options);
  }

  private static void ShowHelp() {
    Console.WriteLine("Universal Test Runner v1.0");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  TestRunner <test.dll> [options]           Run tests for a single assembly");
    Console.WriteLine("  TestRunner <directory> --all [options]    Run all framework variants");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --filter <pattern>     Filter tests by name pattern");
    Console.WriteLine("  --all                  Run all framework variants found in directory");
    Console.WriteLine("  --parallel             Run frameworks in parallel");
    Console.WriteLine("  --frameworks <list>    Comma-separated list of frameworks to run");
    Console.WriteLine("  --verbose              Show detailed output");
    Console.WriteLine("  --help                 Show this help");
    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.WriteLine("  TestRunner Tests/bin/Release/net20/Tests.dll");
    Console.WriteLine("  TestRunner Tests/bin/Release --all --parallel");
    Console.WriteLine("  TestRunner Tests/bin/Release --frameworks net20,net48,net9.0");
  }

  #region Argument Parsing

  private class Options {
    public List<string> AssemblyPaths { get; } = new();
    public string? TestDirectory { get; set; }
    public string? Filter { get; set; }
    public bool RunAll { get; set; }
    public bool Parallel { get; set; }
    public bool Verbose { get; set; }
    public bool WorkerMode { get; set; }
    public bool ShowHelp { get; set; }
    public List<string> Frameworks { get; } = new();
  }

  private static Options ParseArgs(string[] args) {
    var options = new Options();

    for (var i = 0; i < args.Length; ++i) {
      var arg = args[i];

      if (arg == "--filter" && i + 1 < args.Length)
        options.Filter = args[++i];
      else if (arg == "--all")
        options.RunAll = true;
      else if (arg == "--parallel")
        options.Parallel = true;
      else if (arg == "--verbose")
        options.Verbose = true;
      else if (arg == "--worker")
        options.WorkerMode = true;
      else if (arg == "--help" || arg == "-h")
        options.ShowHelp = true;
      else if (arg == "--frameworks" && i + 1 < args.Length) {
        var fws = args[++i].Split(',');
        foreach (var fw in fws)
          options.Frameworks.Add(fw.Trim());
      } else if (!arg.StartsWith("-")) {
        if (Directory.Exists(arg))
          options.TestDirectory = arg;
        else if (File.Exists(arg))
          options.AssemblyPaths.Add(arg);
        else
          Console.WriteLine($"Warning: Path not found: {arg}");
      }
    }

    return options;
  }

  #endregion

  #region Worker Mode

  private static int RunAsWorker(Options options) {
    if (options.AssemblyPaths.Count == 0) {
      Console.WriteLine("{{\"error\":\"No assembly specified\"}}");
      return 1;
    }

    var assemblyPath = options.AssemblyPaths[0];
    if (!File.Exists(assemblyPath)) {
      Console.WriteLine($"{{\"error\":\"Assembly not found: {EscapeJson(assemblyPath)}\"}}");
      return 1;
    }

    try {
      var runner = new TestRunner(assemblyPath, options.Filter, options.Verbose);
      var result = runner.Run();

      // Output JSON result
      Console.WriteLine(result.ToJson());

      return result.Failed > 0 ? 1 : 0;
    } catch (Exception ex) {
      Console.WriteLine($"{{\"error\":\"{EscapeJson(ex.Message)}\"}}");
      return 1;
    }
  }

  #endregion

  #region Orchestrator Mode

  // Frameworks to discover and test
  // Note: netstandard is excluded - it's a specification, not a runtime.
  // netstandard builds are verified by compilation, but can't be tested canonically
  // because any host runtime's BCL types take precedence over polyfills.
  private static readonly string[] KnownFrameworks = {
    "net20", "net35", "net40", "net45",
    "net461", "net462", "net47", "net471", "net472", "net48",
    "netcoreapp3.1",
    "net5.0", "net6.0", "net7.0", "net8.0", "net9.0", "net10.0"
  };

  private static int RunAsOrchestrator(Options options) {
    Console.WriteLine("Universal Test Runner v1.0");
    Console.WriteLine(new string('=', 70));
    Console.WriteLine();

    var assemblies = new List<(string Framework, string Path)>();

    // Collect assemblies to test
    if (options.TestDirectory != null && options.RunAll) {
      assemblies = DiscoverAssemblies(options.TestDirectory, options.Frameworks);
    } else if (options.AssemblyPaths.Count > 0) {
      foreach (var path in options.AssemblyPaths) {
        var fw = DetectFramework(path);
        assemblies.Add((fw, path));
      }
    }

    if (assemblies.Count == 0) {
      Console.WriteLine("No test assemblies found.");
      return 1;
    }

    Console.WriteLine($"Found {assemblies.Count} test assembly(ies):");
    foreach (var (fw, path) in assemblies)
      Console.WriteLine($"  [{fw}] {Path.GetFileName(path)}");
    Console.WriteLine();

    // Run tests
    var results = new List<(string Framework, TestResult Result)>();
    var stopwatch = Stopwatch.StartNew();

    if (options.Parallel) {
      Console.WriteLine("Running tests in parallel...");
      Console.WriteLine();

      var tasks = assemblies.Select(a => Task.Run(() => {
        var result = RunWorkerForAssembly(a.Framework, a.Path, options);
        return (a.Framework, result);
      })).ToArray();

      // Wait for all tasks (compatible with all frameworks)
      foreach (var task in tasks)
        task.Wait();
      results.AddRange(tasks.Select(t => t.Result));
    } else {
      foreach (var (fw, path) in assemblies) {
        Console.WriteLine($"Running [{fw}]...");
        var result = RunWorkerForAssembly(fw, path, options);
        results.Add((fw, result));

        // Print summary for this framework
        if (result.Error != null)
          Console.WriteLine($"  ERROR: {result.Error}");
        else
          Console.WriteLine($"  Passed: {result.Passed}, Failed: {result.Failed}, Skipped: {result.Skipped}");
        Console.WriteLine();
      }
    }

    stopwatch.Stop();

    // Print final summary
    PrintSummary(results, stopwatch.Elapsed);

    // Return failure if any tests failed
    return results.Any(r => r.Result.Failed > 0 || r.Result.Error != null) ? 1 : 0;
  }

  private static List<(string Framework, string Path)> DiscoverAssemblies(string directory, List<string> filterFrameworks) {
    var results = new List<(string Framework, string Path)>();

    foreach (var fw in KnownFrameworks) {
      if (filterFrameworks.Count > 0 && !filterFrameworks.Contains(fw))
        continue;

      var fwDir = Path.Combine(directory, fw);
      if (!Directory.Exists(fwDir))
        continue;

      // Look for test DLLs
      var dlls = Directory.GetFiles(fwDir, "*.Tests.dll")
        .Concat(Directory.GetFiles(fwDir, "*Tests.dll"))
        .Distinct()
        .ToList();

      foreach (var dll in dlls)
        results.Add((fw, dll));
    }

    return results;
  }

  private static string DetectFramework(string assemblyPath) {
    // Try to detect from path
    var dir = Path.GetDirectoryName(assemblyPath) ?? "";
    var dirName = Path.GetFileName(dir);

    foreach (var fw in KnownFrameworks) {
      if (dirName.Equals(fw, StringComparison.OrdinalIgnoreCase))
        return fw;
    }

    return "unknown";
  }

  private static TestResult RunWorkerForAssembly(string framework, string assemblyPath, Options options) {
    // Determine which runner to use based on framework
    var runnerPath = GetRunnerForFramework(framework);

    if (runnerPath == null || !File.Exists(runnerPath)) {
      // Fall back to running directly if we're compatible
      if (CanRunDirectly(framework)) {
        var runner = new TestRunner(assemblyPath, options.Filter, options.Verbose);
        return runner.Run();
      }
      return new TestResult { Error = $"No runner available for {framework}" };
    }

    // Spawn worker process
    return SpawnWorker(runnerPath, framework, assemblyPath, options);
  }

  private static string? GetRunnerForFramework(string framework) {
    // Get the directory where the orchestrator is running from
    var baseDir = AppDomain.CurrentDomain.BaseDirectory;
    var parentDir = Path.GetDirectoryName(baseDir.TrimEnd(Path.DirectorySeparatorChar));

    if (parentDir == null)
      return null;

    // Map framework to runner directory
    var runnerFw = MapToRunnerFramework(framework);
    var runnerDir = Path.Combine(parentDir, runnerFw);

    // Look for TestRunner.exe or TestRunner.dll
    var exePath = Path.Combine(runnerDir, "TestRunner.exe");
    if (File.Exists(exePath))
      return exePath;

    var dllPath = Path.Combine(runnerDir, "TestRunner.dll");
    if (File.Exists(dllPath))
      return dllPath;

    return null;
  }

  private static string MapToRunnerFramework(string framework) {
    // Map test framework to compatible runner framework
    // netstandard can't be run directly, use the first runtime that fully supports it
    if (framework == "netstandard2.0")
      return "net461"; // net461 is the first .NET Framework to fully implement netstandard2.0
    if (framework == "netstandard2.1")
      return "netcoreapp3.1"; // netcoreapp3.1 is the first to implement netstandard2.1

    // For canonical testing, each framework should use its own runner
    // This ensures polyfills are tested correctly without CLR type unification issues
    return framework;
  }

  private static bool CanRunDirectly(string framework) {
    var currentFw = GetCurrentFramework();

    // ONLY run directly when framework matches exactly
    // This ensures polyfills are tested with the correct CLR, not bypassed
    // by running on a newer CLR that has the real BCL types
    return currentFw == framework;
  }

  private static string GetCurrentFramework() {
#if NET9_0_OR_GREATER
    return "net9.0";
#elif NET8_0
    return "net8.0";
#elif NET7_0
    return "net7.0";
#elif NET6_0
    return "net6.0";
#elif NET5_0
    return "net5.0";
#elif NETCOREAPP3_1
    return "netcoreapp3.1";
#elif NET48
    return "net48";
#elif NET472
    return "net472";
#elif NET471
    return "net471";
#elif NET47
    return "net47";
#elif NET462
    return "net462";
#elif NET461
    return "net461";
#elif NET45
    return "net45";
#elif NET40
    return "net40";
#elif NET35
    return "net35";
#elif NET20
    return "net20";
#else
    return "unknown";
#endif
  }

  private static TestResult SpawnWorker(string runnerPath, string framework, string assemblyPath, Options options) {
    try {
      var psi = new ProcessStartInfo();

      if (runnerPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) {
        psi.FileName = "dotnet";
        psi.Arguments = $"\"{runnerPath}\" \"{assemblyPath}\" --worker";
      } else {
        psi.FileName = runnerPath;
        psi.Arguments = $"\"{assemblyPath}\" --worker";
      }

      if (options.Filter != null)
        psi.Arguments += $" --filter \"{options.Filter}\"";
      if (options.Verbose)
        psi.Arguments += " --verbose";

      psi.UseShellExecute = false;
      psi.RedirectStandardOutput = true;
      psi.RedirectStandardError = true;
      psi.CreateNoWindow = true;

      using var process = Process.Start(psi);
      if (process == null)
        return new TestResult { Error = "Failed to start worker process" };

      var output = process.StandardOutput.ReadToEnd();
      var error = process.StandardError.ReadToEnd();
      process.WaitForExit();

      // Parse JSON result from last line
      var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
      var jsonLine = lines.LastOrDefault(l => l.TrimStart().StartsWith("{"));

      if (jsonLine != null)
        return TestResult.FromJson(jsonLine);

      if (!string.IsNullOrEmpty(error))
        return new TestResult { Error = error };

      return new TestResult { Error = "No result from worker" };
    } catch (Exception ex) {
      return new TestResult { Error = ex.Message };
    }
  }

  private static void PrintSummary(List<(string Framework, TestResult Result)> results, TimeSpan elapsed) {
    Console.WriteLine();
    Console.WriteLine(new string('=', 70));
    Console.WriteLine("Test Run Summary");
    Console.WriteLine(new string('=', 70));
    Console.WriteLine();

    // Table header
    Console.WriteLine($"{"Framework",-16} {"Passed",8} {"Failed",8} {"Skipped",8} {"Status",-10}");
    Console.WriteLine(new string('-', 70));

    var totalPassed = 0;
    var totalFailed = 0;
    var totalSkipped = 0;
    var failedFrameworks = new List<string>();

    foreach (var (fw, result) in results.OrderBy(r => GetFrameworkOrder(r.Framework))) {
      string status;
      if (result.Error != null) {
        status = "ERROR";
        failedFrameworks.Add(fw);
      } else if (result.Failed > 0) {
        status = "FAIL";
        failedFrameworks.Add(fw);
      } else {
        status = "PASS";
      }

      Console.WriteLine($"{fw,-16} {result.Passed,8} {result.Failed,8} {result.Skipped,8} {status,-10}");

      totalPassed += result.Passed;
      totalFailed += result.Failed;
      totalSkipped += result.Skipped;
    }

    Console.WriteLine(new string('-', 70));
    Console.WriteLine($"{"TOTAL",-16} {totalPassed,8} {totalFailed,8} {totalSkipped,8}");
    Console.WriteLine();
    Console.WriteLine($"Duration: {elapsed.TotalSeconds:F2}s");
    Console.WriteLine();

    // Print failures detail
    if (failedFrameworks.Count > 0) {
      Console.WriteLine("Frameworks with failures:");
      foreach (var fw in failedFrameworks) {
        var result = results.First(r => r.Framework == fw).Result;
        Console.WriteLine($"  [{fw}]");
        if (result.Error != null)
          Console.WriteLine($"    Error: {result.Error}");
        foreach (var failure in result.Failures.Take(5)) {
          Console.WriteLine($"    - {failure.Name}");
          Console.WriteLine($"      {failure.Message}");
        }
        if (result.Failures.Count > 5)
          Console.WriteLine($"    ... and {result.Failures.Count - 5} more");
        Console.WriteLine();
      }
    }
  }

  private static int GetFrameworkOrder(string fw) {
    var order = new[] {
      "net20", "net35", "net40", "net45",
      "net461", "net462", "net47", "net471", "net472", "net48",
      "netcoreapp3.1",
      "net5.0", "net6.0", "net7.0", "net8.0", "net9.0", "net10.0"
    };
    var idx = Array.IndexOf(order, fw);
    return idx >= 0 ? idx : 100;
  }

  #endregion

  #region Test Runner (Worker)

  internal class TestResult {
    public int Passed { get; set; }
    public int Failed { get; set; }
    public int Skipped { get; set; }
    public double DurationSeconds { get; set; }
    public string? Error { get; set; }
    public List<TestFailure> Failures { get; set; } = new();

    public string ToJson() {
      var sb = new StringBuilder();
      sb.Append("{");
      sb.Append($"\"passed\":{Passed},");
      sb.Append($"\"failed\":{Failed},");
      sb.Append($"\"skipped\":{Skipped},");
      sb.Append($"\"duration\":{DurationSeconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)},");

      if (Error != null)
        sb.Append($"\"error\":\"{EscapeJson(Error)}\",");

      sb.Append("\"failures\":[");
      for (var i = 0; i < Failures.Count; ++i) {
        if (i > 0) sb.Append(",");
        var f = Failures[i];
        sb.Append("{");
        sb.Append($"\"name\":\"{EscapeJson(f.Name)}\",");
        sb.Append($"\"message\":\"{EscapeJson(f.Message)}\"");
        sb.Append("}");
      }
      sb.Append("]");

      sb.Append("}");
      return sb.ToString();
    }

    public static TestResult FromJson(string json) {
      var result = new TestResult();
      try {
        // Simple JSON parsing (no external dependencies)
        result.Passed = ExtractInt(json, "passed");
        result.Failed = ExtractInt(json, "failed");
        result.Skipped = ExtractInt(json, "skipped");
        result.DurationSeconds = ExtractDouble(json, "duration");
        result.Error = ExtractString(json, "error");

        // Parse failures array
        var failuresStart = json.IndexOf("\"failures\":[", StringComparison.Ordinal);
        if (failuresStart >= 0) {
          var arrStart = json.IndexOf('[', failuresStart);
          var arrEnd = json.IndexOf(']', arrStart);
          if (arrStart >= 0 && arrEnd >= 0) {
            var arrContent = json.Substring(arrStart + 1, arrEnd - arrStart - 1);
            // Parse individual failure objects
            var depth = 0;
            var objStart = -1;
            for (var i = 0; i < arrContent.Length; ++i) {
              if (arrContent[i] == '{') {
                if (depth == 0) objStart = i;
                ++depth;
              } else if (arrContent[i] == '}') {
                --depth;
                if (depth == 0 && objStart >= 0) {
                  var objStr = arrContent.Substring(objStart, i - objStart + 1);
                  result.Failures.Add(new TestFailure {
                    Name = ExtractString(objStr, "name") ?? "",
                    Message = ExtractString(objStr, "message") ?? ""
                  });
                  objStart = -1;
                }
              }
            }
          }
        }
      } catch {
        result.Error = "Failed to parse worker result";
      }
      return result;
    }

    private static int ExtractInt(string json, string key) {
      var pattern = $"\"{key}\":";
      var idx = json.IndexOf(pattern, StringComparison.Ordinal);
      if (idx < 0) return 0;
      idx += pattern.Length;
      var end = idx;
      while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '-'))
        ++end;
      if (end > idx && int.TryParse(json.Substring(idx, end - idx), out var val))
        return val;
      return 0;
    }

    private static double ExtractDouble(string json, string key) {
      var pattern = $"\"{key}\":";
      var idx = json.IndexOf(pattern, StringComparison.Ordinal);
      if (idx < 0) return 0;
      idx += pattern.Length;
      var end = idx;
      while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '.' || json[end] == '-'))
        ++end;
      if (end > idx && double.TryParse(json.Substring(idx, end - idx),
          System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var val))
        return val;
      return 0;
    }

    private static string? ExtractString(string json, string key) {
      var pattern = $"\"{key}\":\"";
      var idx = json.IndexOf(pattern, StringComparison.Ordinal);
      if (idx < 0) return null;
      idx += pattern.Length;
      var end = idx;
      while (end < json.Length) {
        if (json[end] == '"' && (end == idx || json[end - 1] != '\\'))
          break;
        ++end;
      }
      if (end > idx)
        return UnescapeJson(json.Substring(idx, end - idx));
      return null;
    }
  }

  internal class TestFailure {
    public string Name { get; set; } = "";
    public string Message { get; set; } = "";
  }

  internal class TestRunner {
    private readonly string _assemblyPath;
    private readonly string? _filter;
    private readonly bool _verbose;
    private int _passed;
    private int _failed;
    private int _skipped;
    private readonly List<TestFailure> _failures = new();
    private readonly Stopwatch _stopwatch = new();

    public TestRunner(string assemblyPath, string? filter, bool verbose) {
      _assemblyPath = Path.GetFullPath(assemblyPath);
      _filter = filter;
      _verbose = verbose;
    }

    public TestResult Run() {
      if (_verbose) {
        Console.Error.WriteLine($"Loading: {_assemblyPath}");
      }

      // Set current directory to assembly location
      var assemblyDir = Path.GetDirectoryName(_assemblyPath)!;
      Environment.CurrentDirectory = assemblyDir;

      // Load assembly
      var assembly = Assembly.LoadFrom(_assemblyPath);

      // Find all test fixtures
      var testFixtures = FindTestFixtures(assembly);

      if (_verbose) {
        Console.Error.WriteLine($"Found {testFixtures.Count} test fixture(s)");
      }

      _stopwatch.Start();

      foreach (var fixture in testFixtures) {
        RunFixture(fixture);
      }

      _stopwatch.Stop();

      return new TestResult {
        Passed = _passed,
        Failed = _failed,
        Skipped = _skipped,
        DurationSeconds = _stopwatch.Elapsed.TotalSeconds,
        Failures = _failures
      };
    }

    private List<Type> FindTestFixtures(Assembly assembly) {
      var fixtures = new List<Type>();

      foreach (var type in assembly.GetTypes()) {
        if (type.IsAbstract || type.IsInterface)
          continue;

        var hasTestFixture = type.GetCustomAttributes(true)
          .Any(a => a.GetType().Name == "TestFixtureAttribute");

        var hasTestMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
          .Any(m => m.GetCustomAttributes(true).Any(a => a.GetType().Name == "TestAttribute"));

        if (hasTestFixture || hasTestMethods) {
          if (_filter == null || (type.FullName != null &&
              type.FullName.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) >= 0)) {
            fixtures.Add(type);
          }
        }
      }

      return fixtures.OrderBy(t => t.FullName).ToList();
    }

    private void RunFixture(Type fixtureType) {
      if (_verbose)
        Console.Error.WriteLine($"[Fixture] {fixtureType.FullName}");

      object? instance;
      try {
        instance = Activator.CreateInstance(fixtureType);
      } catch (Exception ex) {
        if (_verbose)
          Console.Error.WriteLine($"  [ERROR] Failed to create fixture: {ex.Message}");
        return;
      }

      var oneTimeSetUp = FindMethod(fixtureType, "OneTimeSetUpAttribute");
      var oneTimeTearDown = FindMethod(fixtureType, "OneTimeTearDownAttribute");
      var setUp = FindMethod(fixtureType, "SetUpAttribute");
      var tearDown = FindMethod(fixtureType, "TearDownAttribute");

      if (oneTimeSetUp != null) {
        try {
          InvokeMethod(oneTimeSetUp, instance!);
        } catch (Exception ex) {
          if (_verbose)
            Console.Error.WriteLine($"  [ERROR] OneTimeSetUp failed: {GetInnerMessage(ex)}");
          return;
        }
      }

      // Test methods
      var testMethods = fixtureType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
        .Where(m => m.GetCustomAttributes(true).Any(a => a.GetType().Name == "TestAttribute"))
        .OrderBy(m => m.Name)
        .ToList();

      foreach (var testMethod in testMethods) {
        if (_filter != null && testMethod.Name.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) < 0
            && fixtureType.FullName!.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) < 0)
          continue;

        RunTest(instance!, testMethod, setUp, tearDown);
      }

      // TestCase methods
      var testCaseMethods = fixtureType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
        .Where(m => m.GetCustomAttributes(true).Any(a => a.GetType().Name == "TestCaseAttribute"))
        .OrderBy(m => m.Name)
        .ToList();

      foreach (var testMethod in testCaseMethods) {
        if (_filter != null && testMethod.Name.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) < 0
            && fixtureType.FullName!.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) < 0)
          continue;

        var testCases = testMethod.GetCustomAttributes(true)
          .Where(a => a.GetType().Name == "TestCaseAttribute")
          .ToList();

        foreach (var testCase in testCases) {
          var arguments = GetTestCaseArguments(testCase);
          RunTestCase(instance!, testMethod, setUp, tearDown, arguments);
        }
      }

      if (oneTimeTearDown != null) {
        try {
          InvokeMethod(oneTimeTearDown, instance!);
        } catch { }
      }

      if (instance is IDisposable disposable) {
        try { disposable.Dispose(); } catch { }
      }
    }

    private MethodInfo? FindMethod(Type type, string attributeName) =>
      type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
        .FirstOrDefault(m => m.GetCustomAttributes(true).Any(a => a.GetType().Name == attributeName));

    private void RunTest(object instance, MethodInfo testMethod, MethodInfo? setUp, MethodInfo? tearDown) {
      var testName = $"{instance.GetType().Name}.{testMethod.Name}";

      // Check for skip attributes
      if (testMethod.GetCustomAttributes(true).Any(a =>
          a.GetType().Name == "IgnoreAttribute" || a.GetType().Name == "ExplicitAttribute")) {
        ++_skipped;
        return;
      }

      // Handle TestCaseSource
      if (testMethod.GetCustomAttributes(true).Any(a => a.GetType().Name == "TestCaseSourceAttribute")) {
        RunTestCaseSource(instance, testMethod, setUp, tearDown);
        return;
      }

      // Skip parameterized tests without TestCase
      if (testMethod.GetParameters().Length > 0) {
        ++_skipped;
        return;
      }

      try {
        if (setUp != null) InvokeMethod(setUp, instance);
        InvokeMethod(testMethod, instance);
        if (tearDown != null) try { InvokeMethod(tearDown, instance); } catch { }
        ++_passed;
      } catch (Exception ex) {
        var innerEx = ex is TargetInvocationException ? ex.InnerException ?? ex : ex;
        var exTypeName = innerEx.GetType().Name;

        if (exTypeName == "SuccessException") {
          ++_passed;
        } else if (exTypeName == "IgnoreException" || exTypeName == "InconclusiveException") {
          ++_skipped;
        } else {
          _failures.Add(new TestFailure { Name = testName, Message = GetInnerMessage(ex) });
          ++_failed;
        }
      }
    }

    private void RunTestCase(object instance, MethodInfo testMethod, MethodInfo? setUp, MethodInfo? tearDown, object?[]? arguments) {
      var parameters = testMethod.GetParameters();
      var argsStr = arguments == null ? "" : string.Join(", ", arguments.Select((a, i) => {
        var name = i < parameters.Length ? parameters[i].Name + ": " : "";
        return name + FormatArgument(a);
      }));
      var testName = $"{instance.GetType().Name}.{testMethod.Name}({argsStr})";

      try {
        if (setUp != null) InvokeMethod(setUp, instance);
        testMethod.Invoke(instance, arguments);
        if (tearDown != null) try { InvokeMethod(tearDown, instance); } catch { }
        ++_passed;
      } catch (Exception ex) {
        var innerEx = ex is TargetInvocationException ? ex.InnerException ?? ex : ex;
        var exTypeName = innerEx.GetType().Name;

        if (exTypeName == "SuccessException") {
          ++_passed;
        } else if (exTypeName == "IgnoreException" || exTypeName == "InconclusiveException") {
          ++_skipped;
        } else {
          _failures.Add(new TestFailure { Name = testName, Message = GetInnerMessage(ex) });
          ++_failed;
        }
      }
    }

    private void RunTestCaseSource(object instance, MethodInfo testMethod, MethodInfo? setUp, MethodInfo? tearDown) {
      var fixtureType = instance.GetType();
      var attrs = testMethod.GetCustomAttributes(true)
        .Where(a => a.GetType().Name == "TestCaseSourceAttribute")
        .ToList();

      foreach (var attr in attrs) {
        var attrType = attr.GetType();
        var sourceName = attrType.GetProperty("SourceName")?.GetValue(attr) as string;
        var sourceType = attrType.GetProperty("SourceType")?.GetValue(attr) as Type ?? fixtureType;

        if (string.IsNullOrEmpty(sourceName)) {
          ++_skipped;
          continue;
        }

        IEnumerable? testCases = null;

        // Try property
        var prop = sourceType.GetProperty(sourceName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
        if (prop != null) {
          var propInstance = prop.GetGetMethod(true)?.IsStatic == true ? null : (sourceType == fixtureType ? instance : null);
          testCases = prop.GetValue(propInstance) as IEnumerable;
        }

        // Try field
        if (testCases == null) {
          var field = sourceType.GetField(sourceName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
          if (field != null) {
            var fieldInstance = field.IsStatic ? null : (sourceType == fixtureType ? instance : null);
            testCases = field.GetValue(fieldInstance) as IEnumerable;
          }
        }

        // Try method
        if (testCases == null) {
          var method = sourceType.GetMethod(sourceName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
          if (method != null) {
            var methodInstance = method.IsStatic ? null : (sourceType == fixtureType ? instance : null);
            testCases = method.Invoke(methodInstance, null) as IEnumerable;
          }
        }

        if (testCases == null) {
          ++_skipped;
          continue;
        }

        foreach (var testCase in testCases) {
          if (testCase == null) {
            ++_skipped;
            continue;
          }
          var arguments = ExtractTestCaseArguments(testCase);
          RunTestCase(instance, testMethod, setUp, tearDown, arguments);
        }
      }
    }

    private static object?[]? ExtractTestCaseArguments(object testCase) {
      var testCaseType = testCase.GetType();
      var argsProp = testCaseType.GetProperty("Arguments");
      if (argsProp != null)
        return argsProp.GetValue(testCase) as object?[];
      if (testCase is object[] argsArray)
        return argsArray;
      if (testCase is IEnumerable enumerable && testCase is not string) {
        var list = new List<object?>();
        foreach (var item in enumerable) list.Add(item);
        return list.ToArray();
      }
      return new object?[] { testCase };
    }

    private static object?[]? GetTestCaseArguments(object attr) {
      return attr.GetType().GetProperty("Arguments")?.GetValue(attr) as object?[];
    }

    private static void InvokeMethod(MethodInfo method, object instance) {
      try {
        var result = method.Invoke(instance, null);
        if (result is Task task)
          task.GetAwaiter().GetResult();
      } catch (TargetInvocationException ex) when (ex.InnerException != null) {
        throw ex.InnerException;
      }
    }

    private static string GetInnerMessage(Exception ex) =>
      ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException.Message : ex.Message;
  }

  #endregion

  #region Helpers

  private const int MaxDisplayLength = 32;

  private static string FormatArgument(object? value) {
    if (value == null)
      return "null";

    switch (value) {
      case string s:
        return s.Length <= MaxDisplayLength
          ? $"\"{s}\""
          : $"\"{Truncate(s)}\" [{s.Length}]";
      case Array arr:
        var elementType = arr.GetType().GetElementType()?.Name ?? "Object";
        if (arr.Length <= 8) {
          var elements = new List<string>();
          foreach (var item in arr)
            elements.Add(FormatArgument(item));
          return $"[{string.Join(", ", elements)}]";
        }
        return $"{elementType}[{arr.Length}]";
      case IEnumerable enumerable when value is not string:
        var items = new List<string>();
        var count = 0;
        foreach (var item in enumerable) {
          if (count < 8)
            items.Add(FormatArgument(item));
          ++count;
        }
        if (count <= 8)
          return $"[{string.Join(", ", items)}]";
        return $"[{string.Join(", ", items)}, ...] [{count}]";
      default:
        var str = value.ToString() ?? "";
        return str.Length <= MaxDisplayLength ? str : $"{Truncate(str)} [{str.Length}]";
    }
  }

  private static string Truncate(string s) {
    if (s.Length <= MaxDisplayLength)
      return s;
    var partLen = (MaxDisplayLength - 4) / 3; // 4 chars for "...."
    var start = s.Substring(0, partLen);
    var mid = s.Substring(s.Length / 2 - partLen / 2, partLen);
    var end = s.Substring(s.Length - partLen);
    return $"{start}..{mid}..{end}";
  }

  private static string EscapeJson(string s) {
    if (string.IsNullOrEmpty(s)) return s;
    var sb = new StringBuilder(s.Length);
    foreach (var c in s) {
      switch (c) {
        case '"': sb.Append("\\\""); break;
        case '\\': sb.Append("\\\\"); break;
        case '\n': sb.Append("\\n"); break;
        case '\r': sb.Append("\\r"); break;
        case '\t': sb.Append("\\t"); break;
        default: sb.Append(c); break;
      }
    }
    return sb.ToString();
  }

  private static string UnescapeJson(string s) {
    if (string.IsNullOrEmpty(s)) return s;
    var sb = new StringBuilder(s.Length);
    for (var i = 0; i < s.Length; ++i) {
      if (s[i] == '\\' && i + 1 < s.Length) {
        switch (s[++i]) {
          case '"': sb.Append('"'); break;
          case '\\': sb.Append('\\'); break;
          case 'n': sb.Append('\n'); break;
          case 'r': sb.Append('\r'); break;
          case 't': sb.Append('\t'); break;
          default: sb.Append(s[i]); break;
        }
      } else {
        sb.Append(s[i]);
      }
    }
    return sb.ToString();
  }

  #endregion
}
