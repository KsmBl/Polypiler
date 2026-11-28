using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using NUnit.Framework;
using Polypiler.Cli;
using Polypiler.Cli.Capabilities;
using Polypiler.Core.Verification.Capabilities;
using Polypiler.Prototype.Testing;

namespace Polypiler.Tests.Cli.Capabilities;

/// <summary>
/// <c>polypiler capabilities</c> (#176): every row of the test design against the fixture reports of
/// <c>tests/fixtures/cli/capabilities</c>, whose expected rows are read off them by hand.
/// </summary>
[TestFixture]
public sealed class CapabilitiesCommandTests {

  private static readonly string _fixtures = Path.Combine(PrototypeFixture.RepositoryRoot, "tests", "fixtures", "cli", "capabilities");

  private static string Fixture(string name) => Path.Combine(_fixtures, name + ".capabilities.json");

  private static (int Exit, string Stdout, string Stderr) Run(params string[] args) {
    using var stdout = new StringWriter();
    using var stderr = new StringWriter();
    var exit = Program.Run(["capabilities", .. args], stdout, stderr);
    return (exit, stdout.ToString(), stderr.ToString());
  }

  private static string[][] Rows(string stdout)
    => [.. stdout.Split('\n').Skip(1).TakeWhile(line => line.Length > 0).Select(line => System.Text.RegularExpressions.Regex.Split(line.Trim(), " {2,}"))];

  [TestCase("--from", "csharp", "csharp")]
  [TestCase("--to", "csharp", "javascript")]
  [TestCase("--source-profile", "es2022", "javascript")]
  [TestCase("--target-profile", "es2022", "csharp")]
  public void EachFilterAlone_KeepsOnlyTheMatchingEvidencedRows(string option, string value, string from) {
    var (exit, stdout, _) = Run(option, value, "--report", Fixture("two-pairs"));

    Assert.That(exit, Is.Zero);
    Assert.That(Rows(stdout).Select(row => row[0]), Is.EqualTo(new[] { from }));
  }

  [Test]
  public void TheJsonOutput_HoldsTheRowsOfTheFixture() {
    var (exit, stdout, _) = Run("--format", "json", "--report", Fixture("two-pairs"));
    var json = JsonNode.Parse(stdout)!;

    Assert.That(exit, Is.Zero);
    Assert.That(json["reportCommit"]!.GetValue<string>(), Is.EqualTo(new string('1', 40)));
    Assert.That(json["pairs"]!.AsArray().Select(pair => $"{pair!["from"]}>{pair["to"]}:{pair["state"]}"), Is.EqualTo(new[] { "csharp>javascript:verified", "javascript>csharp:emitted" }));
    var planned = JsonNode.Parse(Run("--format", "json", "--from", "kotlin", "--to", "perl", "--report", Fixture("empty")).Stdout)!["pairs"]![0]!;
    Assert.That(planned["evidence"], Is.Null);
  }

  [Test]
  public void WithoutReport_TheEmbeddedReportIsRead() {
    var (exit, stdout, _) = Run();
    var embedded = EmbeddedCapabilities.Load();

    Assert.That(exit, Is.Zero);
    Assert.That(stdout, Does.EndWith($"{embedded.Pairs.Count(pair => pair.Evidence is not null)} pair(s) with evidence; every other pair is planned\n"));
  }

  [Test]
  public void AReportWithoutEvidence_ListsTheHeaderAndTheSummary() {
    var (_, stdout, _) = Run("--report", Fixture("empty"));

    Assert.That(stdout, Is.EqualTo("FROM  SOURCE-PROFILE  TO  TARGET-PROFILE  STATE  EVIDENCE\n\n0 pair(s) with evidence; every other pair is planned\n"));
  }

  [Test]
  public void All_ListsEveryOrderedPairOfTheLanguages() {
    var empty = Rows(Run("--all", "--report", Fixture("empty")).Stdout);
    var mixed = Rows(Run("--all", "--from", "csharp", "--report", Fixture("two-pairs")).Stdout);

    Assert.That(empty, Has.Length.EqualTo(16));
    Assert.That(Rows(Run("--all", "--to", "perl", "--report", Fixture("empty")).Stdout).Select(row => row[0]), Is.EqualTo(new[] { "csharp", "javascript", "kotlin", "perl" }));
    Assert.That(empty.Select(row => row[4]), Is.All.EqualTo("planned"));
    Assert.That(mixed.Select(row => $"{row[2]}:{row[4]}"), Is.EqualTo(new[] { "csharp:planned", "javascript:verified", "kotlin:planned", "perl:planned" }));
  }

  [Test]
  public void OnlyAFromFilterWithNothingEvidenced_ListsNoRow()
    => Assert.That(Rows(Run("--from", "kotlin", "--report", Fixture("two-pairs")).Stdout), Is.Empty);

  [Test]
  public void ALanguageWithItself_IsOnePlannedRow()
    => Assert.That(Rows(Run("--from", "csharp", "--to", "csharp", "--report", Fixture("two-pairs")).Stdout).Single(), Is.EqualTo(new[] { "csharp", "-", "csharp", "-", "planned", "-" }));

  [TestCase("--from", "klingon", "'klingon' is not a valid value for '--from'; expected one of: csharp, javascript, kotlin, perl")]
  [TestCase("--to", "klingon", "'klingon' is not a valid value for '--to'")]
  [TestCase("--target-profile", "jvm9", "'jvm9' is not a valid value for '--target-profile'")]
  [TestCase("--format", "xml", "'xml' is not a valid value for '--format'")]
  public void AnUnknownValue_IsAUsageError(string option, string value, string message) {
    var (exit, stdout, stderr) = Run(option, value, "--report", Fixture("two-pairs"));

    Assert.That((exit, stdout), Is.EqualTo((2, "")));
    Assert.That(stderr, Does.Contain("error PLY0003: " + message));
  }

  [Test]
  public void AProfileTheLanguageDoesNotHave_IsAUsageError() {
    var (exit, _, stderr) = Run("--from", "csharp", "--source-profile", "perl5", "--report", Fixture("two-pairs"));

    Assert.That(exit, Is.EqualTo(2));
    Assert.That(stderr, Does.Contain("'perl5' is not a valid value for '--source-profile'; expected one of: net10"));
  }

  [Test]
  public void AMissingReport_IsUnreadable() {
    var (exit, _, stderr) = Run("--report", "no/such/report.json");

    Assert.That(exit, Is.EqualTo(1));
    Assert.That(stderr, Does.Contain("error PLY0004: input 'no/such/report.json' does not exist or is not readable"));
  }

  [Test]
  public void AnUnbackedClaim_IsNotShown() {
    var (exit, stdout, stderr) = Run("--report", Fixture("unbacked"));

    Assert.That((exit, stdout), Is.EqualTo((1, "")));
    Assert.That(stderr, Does.Contain("error PLY0061: the report claims kotlin/jvm -> perl/perl5 at 'verified' without evidence"));
  }

  private string _directory = "";

  [SetUp]
  public void CreateDirectory() => this._directory = Directory.CreateTempSubdirectory("polypiler-capabilities-").FullName;

  [TearDown]
  public void DeleteDirectory() => Directory.Delete(this._directory, true);

  private string Write(string text) {
    var path = Path.Combine(this._directory, Guid.NewGuid().ToString("N") + ".json");
    File.WriteAllText(path, text);
    return path;
  }

  [TestCase("not json at all")]
  [TestCase("{\"schemaVersion\": 2}")]
  public void AFileThatIsNoReport_IsUnreadable(string text) {
    var (exit, _, stderr) = Run("--report", this.Write(text));

    Assert.That(exit, Is.EqualTo(1));
    Assert.That(stderr, Does.Contain("error PLY0004: '").And.Contain("is no capability report"));
  }

  [Test]
  public void EveryState_IsPrintedVerbatim() {
    var report = JsonNode.Parse(File.ReadAllText(Fixture("empty")))!;
    var states = report["states"]!.AsArray().Select(state => state!.GetValue<string>()).ToList();
    var pairs = report["pairs"]!.AsArray();
    for (var index = 1; index < states.Count; ++index) {
      pairs[index - 1]!["state"] = states[index];
      pairs[index - 1]!["fixtures"] = 1;
      pairs[index - 1]!["evidence"] = $"gate-{index}.json";
    }

    var rows = Rows(Run("--report", this.Write(report.ToJsonString())).Stdout);
    Assert.That(rows.Select(row => row[4]).Order(StringComparer.Ordinal), Is.EqualTo(states.Skip(1).Order(StringComparer.Ordinal)));
  }

  [TestCase("not json", null)]
  [TestCase("[1]", null)]
  [TestCase("{\"pairs\": 3}", null)]
  [TestCase("{\"pairs\": [{\"state\": \"planned\", \"evidence\": null}]}", null)]
  [TestCase("{\"pairs\": [{\"state\": \"verified\", \"evidence\": \"g.json\"}]}", null)]
  [TestCase("{\"pairs\": [{\"state\": \"verified\"}]}", "the report claims ?/? -> ?/? at 'verified' without evidence")]
  [TestCase("{\"pairs\": [null]}", "the report claims ?/? -> ?/? at '?' without evidence")]
  public void AnUnbackedClaim_IsFoundInTheText(string text, string? claim) => Assert.That(CapabilitiesCommand.Unbacked(text), Is.EqualTo(claim));

  [Test]
  public void TheRules_AnswerForEveryInput() {
    var report = CapabilityReport.Parse(File.ReadAllText(Fixture("two-pairs")), out _)!;

    Assert.That(CapabilityQuery.Languages(report), Is.EqualTo(new[] { "csharp", "javascript", "kotlin", "perl" }));
    Assert.That(CapabilityQuery.Invalid(report, "csharp", "perl", "net10", "perl5"), Is.Null);
    Assert.That(new CapabilityQuery().Select(report, null, null, null, null, false), Has.Count.EqualTo(2));
    Assert.That(CapabilitiesCommand.Text([]), Does.StartWith("FROM  SOURCE-PROFILE"));
    Assert.Throws<ArgumentNullException>(() => CapabilityQuery.Languages(null!));
    Assert.Throws<ArgumentNullException>(() => CapabilityQuery.Invalid(null!, null, null, null, null));
    Assert.Throws<ArgumentNullException>(() => new CapabilityQuery().Select(null!, null, null, null, null, false));
    Assert.Throws<ArgumentNullException>(() => CapabilitiesCommand.Unbacked(null!));
    Assert.Throws<ArgumentNullException>(() => CapabilitiesCommand.Text(null!));
    Assert.Throws<ArgumentNullException>(() => CapabilitiesCommand.Json(null!, []));
    Assert.Throws<ArgumentNullException>(() => CapabilitiesCommand.Json(report, null!));
    var command = new CapabilitiesCommand();
    Assert.Throws<ArgumentNullException>(() => command.Configure(null!));
    Assert.Throws<ArgumentNullException>(() => command.Execute(null!, null!));
    Assert.That(command.Description, Does.StartWith("list the source and target pairs"));
  }

}
