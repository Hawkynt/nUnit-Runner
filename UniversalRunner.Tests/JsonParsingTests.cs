using System;
using System.Reflection;
using NUnit.Framework;

namespace UniversalRunner.Tests;

[TestFixture]
public class JsonParsingTests {

  [Test]
  public void TestResult_ToJson_ProducesValidJson() {
    var result = CreateTestResult(10, 2, 1, 1.5);
    result.Failures.Add(CreateTestFailure("Test1", "Failed assertion"));

    var json = InvokeToJson(result);

    Assert.That(json, Does.Contain("\"passed\":10"));
    Assert.That(json, Does.Contain("\"failed\":2"));
    Assert.That(json, Does.Contain("\"skipped\":1"));
    Assert.That(json, Does.Contain("\"duration\":1.500"));
    Assert.That(json, Does.Contain("\"failures\":["));
    Assert.That(json, Does.Contain("\"name\":\"Test1\""));
    Assert.That(json, Does.Contain("\"message\":\"Failed assertion\""));
  }

  [Test]
  public void TestResult_FromJson_ParsesValidJson() {
    var json = "{\"passed\":5,\"failed\":1,\"skipped\":2,\"duration\":0.500,\"failures\":[{\"name\":\"MyTest\",\"message\":\"Expected true\"}]}";

    var result = InvokeFromJson(json);

    Assert.That(GetPassed(result), Is.EqualTo(5));
    Assert.That(GetFailed(result), Is.EqualTo(1));
    Assert.That(GetSkipped(result), Is.EqualTo(2));
    Assert.That(GetDuration(result), Is.EqualTo(0.5).Within(0.001));
    Assert.That(GetFailures(result), Has.Count.EqualTo(1));
  }

  [Test]
  public void TestResult_RoundTrip_PreservesData() {
    var original = CreateTestResult(100, 5, 3, 12.345);
    original.Failures.Add(CreateTestFailure("FailedTest", "Something went wrong"));
    original.Failures.Add(CreateTestFailure("AnotherFail", "Unexpected value"));

    var json = InvokeToJson(original);
    var parsed = InvokeFromJson(json);

    Assert.That(GetPassed(parsed), Is.EqualTo(100));
    Assert.That(GetFailed(parsed), Is.EqualTo(5));
    Assert.That(GetSkipped(parsed), Is.EqualTo(3));
    Assert.That(GetFailures(parsed), Has.Count.EqualTo(2));
  }

  [Test]
  public void EscapeJson_HandlesSpecialCharacters() {
    var result = CreateTestResult(0, 1, 0, 0);
    result.Failures.Add(CreateTestFailure("Test", "Line1\nLine2\tTabbed \"quoted\""));

    var json = InvokeToJson(result);

    Assert.That(json, Does.Contain("\\n"));
    Assert.That(json, Does.Contain("\\t"));
    Assert.That(json, Does.Contain("\\\"quoted\\\""));
  }

  #region Reflection Helpers

  private static readonly Type ProgramType = typeof(UniversalTestRunner.Program).Assembly
    .GetType("UniversalTestRunner.Program")!;
  private static readonly Type TestResultType = ProgramType.GetNestedType("TestResult", BindingFlags.NonPublic)!;
  private static readonly Type TestFailureType = ProgramType.GetNestedType("TestFailure", BindingFlags.NonPublic)!;

  private static dynamic CreateTestResult(int passed, int failed, int skipped, double duration) {
    dynamic result = Activator.CreateInstance(TestResultType)!;
    result.Passed = passed;
    result.Failed = failed;
    result.Skipped = skipped;
    result.DurationSeconds = duration;
    return result;
  }

  private static dynamic CreateTestFailure(string name, string message) {
    dynamic failure = Activator.CreateInstance(TestFailureType)!;
    failure.Name = name;
    failure.Message = message;
    return failure;
  }

  private static string InvokeToJson(dynamic result) => result.ToJson();

  private static dynamic InvokeFromJson(string json) {
    var method = TestResultType.GetMethod("FromJson", BindingFlags.Public | BindingFlags.Static)!;
    return method.Invoke(null, new object[] { json })!;
  }

  private static int GetPassed(dynamic result) => result.Passed;
  private static int GetFailed(dynamic result) => result.Failed;
  private static int GetSkipped(dynamic result) => result.Skipped;
  private static double GetDuration(dynamic result) => result.DurationSeconds;
  private static dynamic GetFailures(dynamic result) => result.Failures;

  #endregion

}
