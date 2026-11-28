using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Polypiler.Prototype.Testing;
using Polypiler.Tests.Acceptance.Support;
using Reqnroll;

namespace Polypiler.Tests.Acceptance.Cli;

/// <summary>The steps of <c>polypiler capabilities</c>: the fixture reports copied into the workspace, and the rows of the text table.</summary>
[Binding, Scope(Feature = "polypiler capabilities")]
public sealed class CapabilitiesSteps(ScenarioWorkspace workspace, CommandLog log) {

  private const string Fixtures = "tests/fixtures/cli/capabilities";

  private string[]? _row;

  /// <summary>Copies the fixtures into the workspace.</summary>
  [BeforeScenario(Order = 100)]
  public void CopyTheFixtures() {
    foreach (var file in Directory.EnumerateFiles(Path.Combine(PrototypeFixture.RepositoryRoot, Fixtures)))
      workspace.Write(Path.GetRelativePath(PrototypeFixture.RepositoryRoot, file).Replace('\\', '/'), File.ReadAllText(file));
  }

  /// <summary>The data rows of the table: the lines between the header and the empty line, split at runs of two or more spaces.</summary>
  private string[][] Rows() {
    var lines = log.Last.Stdout.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
    Assert.That(lines[0], Does.StartWith("FROM"), log.Last.Output);
    return [.. lines.Skip(1).TakeWhile(line => line.Length > 0).Select(line => Regex.Split(line.Trim(), " {2,}"))];
  }

  [Then("^stdout has exactly one data row with the cells (.+)$")]
  public void OneRow(string cells) {
    var expected = Regex.Matches(cells, StepText.Quoted).Select(match => match.Groups[1].Value).ToArray();
    this._row = this.Rows().Single();
    Assert.That(this._row.Take(expected.Length), Is.EqualTo(expected), log.Last.Output);
  }

  [Then("^that row's evidence cell is not " + StepText.Quoted + "$")]
  public void Evidence(string text) => Assert.That(this._row![5], Is.Not.EqualTo(text));

  [Then("^stdout has exactly one data row with state " + StepText.Quoted + "$")]
  public void State(string state) => Assert.That(this.Rows().Single()[4], Is.EqualTo(state), log.Last.Output);

  [Then("^stdout has exactly (\\d+) data rows, " + StepText.Quoted + " first$")]
  public void Count(int count, string first) {
    var rows = this.Rows();
    Assert.That((rows.Length, rows[0][0]), Is.EqualTo((count, first)), log.Last.Output);
  }

  [Then("^the final line of stdout is " + StepText.Quoted + "$")]
  public void FinalLine(string text)
    => Assert.That(log.Last.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries).Last().TrimEnd('\r'), Is.EqualTo(StepText.Unescape(text)), log.Last.Output);

  [Then("^stdout is empty$")]
  public void Empty() => Assert.That(log.Last.Stdout, Is.Empty, log.Last.Output);

}
