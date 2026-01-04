using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

namespace UniversalRunner.Tests;

[TestFixture]
public class WhereFilterTests {

  #region Lexer Tests

  [Test]
  public void Lexer_TokenizesSimpleExpression() {
    var tokens = Tokenize("class == MyClass");

    Assert.That(tokens, Has.Count.EqualTo(4));
    Assert.That(GetTokenType(tokens[0]), Is.EqualTo("Class"));
    Assert.That(GetTokenType(tokens[1]), Is.EqualTo("Equal"));
    Assert.That(GetTokenType(tokens[2]), Is.EqualTo("Identifier"));
    Assert.That(GetTokenValue(tokens[2]), Is.EqualTo("MyClass"));
    Assert.That(GetTokenType(tokens[3]), Is.EqualTo("Eof"));
  }

  [Test]
  public void Lexer_TokenizesRegexPattern() {
    var tokens = Tokenize("method =~ /Test.*/");

    Assert.That(tokens, Has.Count.EqualTo(4));
    Assert.That(GetTokenType(tokens[0]), Is.EqualTo("Method"));
    Assert.That(GetTokenType(tokens[1]), Is.EqualTo("RegexMatch"));
    Assert.That(GetTokenType(tokens[2]), Is.EqualTo("RegexLiteral"));
    Assert.That(GetTokenValue(tokens[2]), Is.EqualTo("Test.*"));
  }

  [Test]
  public void Lexer_TokenizesQuotedString() {
    var tokens = Tokenize("cat == \"Unit Test\"");

    Assert.That(tokens, Has.Count.EqualTo(4));
    Assert.That(GetTokenType(tokens[2]), Is.EqualTo("String"));
    Assert.That(GetTokenValue(tokens[2]), Is.EqualTo("Unit Test"));
  }

  [Test]
  public void Lexer_TokenizesBooleanOperators() {
    var tokens = Tokenize("cat == Unit and method != Slow or not class == Skip");

    var types = new List<string>();
    foreach (var token in tokens)
      types.Add(GetTokenType(token));

    Assert.That(types, Does.Contain("And"));
    Assert.That(types, Does.Contain("Or"));
    Assert.That(types, Does.Contain("Not"));
  }

  [Test]
  public void Lexer_TokenizesParentheses() {
    var tokens = Tokenize("(cat == Unit)");

    Assert.That(GetTokenType(tokens[0]), Is.EqualTo("LeftParen"));
    Assert.That(GetTokenType(tokens[4]), Is.EqualTo("RightParen"));
  }

  #endregion

  #region Parser Tests

  [Test]
  public void Parser_ParsesSimpleEquality() {
    var expr = Parse("class == MyClass");
    Assert.That(expr, Is.Not.Null);
  }

  [Test]
  public void Parser_ParsesRegexMatch() {
    var expr = Parse("method =~ /Test.*/");
    Assert.That(expr, Is.Not.Null);
  }

  [Test]
  public void Parser_ParsesAndExpression() {
    var expr = Parse("cat == Unit and method == MyTest");
    Assert.That(expr, Is.Not.Null);
  }

  [Test]
  public void Parser_ParsesOrExpression() {
    var expr = Parse("cat == Unit or cat == Integration");
    Assert.That(expr, Is.Not.Null);
  }

  [Test]
  public void Parser_ParsesNotExpression() {
    var expr = Parse("not method == Slow");
    Assert.That(expr, Is.Not.Null);
  }

  [Test]
  public void Parser_ParsesComplexExpression() {
    var expr = Parse("(cat == Unit or cat == Integration) and not method =~ /Slow.*/");
    Assert.That(expr, Is.Not.Null);
  }

  [Test]
  public void Parser_ThrowsOnInvalidSyntax() {
    Assert.Throws<TargetInvocationException>(() => Parse("class invalid"));
  }

  #endregion

  #region Evaluator Tests

  [Test]
  public void Evaluator_ClassEquality_Matches() {
    var expr = Parse("class == MyClass");
    var ctx = CreateContext("MyNamespace.MyClass", "MyClass", "TestMethod");

    Assert.That(Evaluate(expr, ctx), Is.True);
  }

  [Test]
  public void Evaluator_ClassEquality_NoMatch() {
    var expr = Parse("class == OtherClass");
    var ctx = CreateContext("MyNamespace.MyClass", "MyClass", "TestMethod");

    Assert.That(Evaluate(expr, ctx), Is.False);
  }

  [Test]
  public void Evaluator_MethodRegex_Matches() {
    var expr = Parse("method =~ /Test.*/");
    var ctx = CreateContext("MyNamespace.MyClass", "MyClass", "TestSomething");

    Assert.That(Evaluate(expr, ctx), Is.True);
  }

  [Test]
  public void Evaluator_MethodRegex_NoMatch() {
    var expr = Parse("method =~ /Test.*/");
    var ctx = CreateContext("MyNamespace.MyClass", "MyClass", "RunSomething");

    Assert.That(Evaluate(expr, ctx), Is.False);
  }

  [Test]
  public void Evaluator_CategoryEquality_Matches() {
    var expr = Parse("cat == Unit");
    var ctx = CreateContext("MyNamespace.MyClass", "MyClass", "TestMethod", "Unit");

    Assert.That(Evaluate(expr, ctx), Is.True);
  }

  [Test]
  public void Evaluator_CategoryEquality_NoMatch() {
    var expr = Parse("cat == Integration");
    var ctx = CreateContext("MyNamespace.MyClass", "MyClass", "TestMethod", "Unit");

    Assert.That(Evaluate(expr, ctx), Is.False);
  }

  [Test]
  public void Evaluator_CategoryEquality_MultipleCategories() {
    var expr = Parse("cat == Integration");
    var ctx = CreateContext("MyNamespace.MyClass", "MyClass", "TestMethod", "Unit", "Integration");

    Assert.That(Evaluate(expr, ctx), Is.True);
  }

  [Test]
  public void Evaluator_NotEqual_Works() {
    var expr = Parse("method != SlowTest");
    var ctx = CreateContext("MyNamespace.MyClass", "MyClass", "FastTest");

    Assert.That(Evaluate(expr, ctx), Is.True);
  }

  [Test]
  public void Evaluator_RegexNotMatch_Works() {
    var expr = Parse("method !~ /Slow.*/");
    var ctx = CreateContext("MyNamespace.MyClass", "MyClass", "FastTest");

    Assert.That(Evaluate(expr, ctx), Is.True);
  }

  [Test]
  public void Evaluator_And_BothTrue() {
    var expr = Parse("class == MyClass and method == TestMethod");
    var ctx = CreateContext("MyNamespace.MyClass", "MyClass", "TestMethod");

    Assert.That(Evaluate(expr, ctx), Is.True);
  }

  [Test]
  public void Evaluator_And_OneFalse() {
    var expr = Parse("class == MyClass and method == OtherMethod");
    var ctx = CreateContext("MyNamespace.MyClass", "MyClass", "TestMethod");

    Assert.That(Evaluate(expr, ctx), Is.False);
  }

  [Test]
  public void Evaluator_Or_OneTrue() {
    var expr = Parse("class == OtherClass or method == TestMethod");
    var ctx = CreateContext("MyNamespace.MyClass", "MyClass", "TestMethod");

    Assert.That(Evaluate(expr, ctx), Is.True);
  }

  [Test]
  public void Evaluator_Or_BothFalse() {
    var expr = Parse("class == OtherClass or method == OtherMethod");
    var ctx = CreateContext("MyNamespace.MyClass", "MyClass", "TestMethod");

    Assert.That(Evaluate(expr, ctx), Is.False);
  }

  [Test]
  public void Evaluator_Not_InvertsTrue() {
    var expr = Parse("not class == OtherClass");
    var ctx = CreateContext("MyNamespace.MyClass", "MyClass", "TestMethod");

    Assert.That(Evaluate(expr, ctx), Is.True);
  }

  [Test]
  public void Evaluator_Not_InvertsFalse() {
    var expr = Parse("not class == MyClass");
    var ctx = CreateContext("MyNamespace.MyClass", "MyClass", "TestMethod");

    Assert.That(Evaluate(expr, ctx), Is.False);
  }

  [Test]
  public void Evaluator_Namespace_Matches() {
    var expr = Parse("namespace == MyNamespace");
    var ctx = CreateContext("MyNamespace.MyClass", "MyClass", "TestMethod");

    Assert.That(Evaluate(expr, ctx), Is.True);
  }

  [Test]
  public void Evaluator_Test_MatchesFullName() {
    var expr = Parse("test =~ /.*MyClass.TestMethod/");
    var ctx = CreateContext("MyNamespace.MyClass", "MyClass", "TestMethod");

    Assert.That(Evaluate(expr, ctx), Is.True);
  }

  [Test]
  public void Evaluator_CaseInsensitive_Works() {
    var expr = Parse("class == myclass");
    var ctx = CreateContext("MyNamespace.MyClass", "MyClass", "TestMethod");

    Assert.That(Evaluate(expr, ctx), Is.True);
  }

  #endregion

  #region Reflection Helpers

  private static readonly Type ProgramType = typeof(UniversalTestRunner.Program).Assembly
    .GetType("UniversalTestRunner.Program")!;
  private static readonly Type LexerType = ProgramType.GetNestedType("WhereLexer", BindingFlags.NonPublic)!;
  private static readonly Type ParserType = ProgramType.GetNestedType("WhereParser", BindingFlags.NonPublic)!;
  private static readonly Type ExprType = ProgramType.GetNestedType("WhereExpr", BindingFlags.NonPublic)!;
  private static readonly Type ContextType = ProgramType.GetNestedType("TestContext", BindingFlags.NonPublic)!;

  private static List<object> Tokenize(string input) {
    var lexer = Activator.CreateInstance(LexerType, input)!;
    var nextTokenMethod = LexerType.GetMethod("NextToken")!;
    var tokens = new List<object>();

    while (true) {
      var token = nextTokenMethod.Invoke(lexer, null)!;
      tokens.Add(token);
      if (GetTokenType(token) == "Eof")
        break;
    }

    return tokens;
  }

  private static string GetTokenType(object token) {
    var typeProp = token.GetType().GetProperty("Type")!;
    return typeProp.GetValue(token)!.ToString()!;
  }

  private static string GetTokenValue(object token) {
    var valueProp = token.GetType().GetProperty("Value")!;
    return (string)valueProp.GetValue(token)!;
  }

  private static object Parse(string input) {
    var parser = Activator.CreateInstance(ParserType, input)!;
    var parseMethod = ParserType.GetMethod("Parse")!;
    return parseMethod.Invoke(parser, null)!;
  }

  private static object CreateContext(string fullClassName, string className, string methodName, params string[] categories) {
    var ctx = Activator.CreateInstance(ContextType)!;
    var ns = fullClassName.Contains(".") ? fullClassName.Substring(0, fullClassName.LastIndexOf('.')) : "";

    ContextType.GetProperty("FullName")!.SetValue(ctx, $"{fullClassName}.{methodName}");
    ContextType.GetProperty("ClassName")!.SetValue(ctx, className);
    ContextType.GetProperty("FullClassName")!.SetValue(ctx, fullClassName);
    ContextType.GetProperty("MethodName")!.SetValue(ctx, methodName);
    ContextType.GetProperty("Namespace")!.SetValue(ctx, ns);

    var categoriesProp = ContextType.GetProperty("Categories")!;
    var categoriesList = categoriesProp.GetValue(ctx)!;
    var addMethod = categoriesList.GetType().GetMethod("Add")!;
    foreach (var cat in categories)
      addMethod.Invoke(categoriesList, new object[] { cat });

    return ctx;
  }

  private static bool Evaluate(object expr, object context) {
    var evaluateMethod = ExprType.GetMethod("Evaluate")!;
    return (bool)evaluateMethod.Invoke(expr, new[] { context })!;
  }

  #endregion
}
