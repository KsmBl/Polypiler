using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Polypiler.Core;
using Polypiler.Core.Verification.Capabilities;

namespace Polypiler.Cli.Capabilities;

/// <summary>
/// The <c>capabilities</c> command: which source and target pairs the capability report states, with
/// their state and evidence (PRD §10, §23.6). The report is the one the executable embeds, or the file
/// <c>--report</c> names; the output is a pure function of the report and the filters.
/// </summary>
public sealed class CapabilitiesCommand : ICliCommand {

  private const string SourceProfile = "--source-profile";
  private const string TargetProfile = "--target-profile";
  private const string All = "--all";
  private const string Format = "--format";
  private const string Report = "--report";

  /// <inheritdoc/>
  public string Name => "capabilities";

  /// <inheritdoc/>
  public string Description => "list the source and target pairs the capability report states, with their state and evidence";

  /// <inheritdoc/>
  public void Configure(Command command) {
    ArgumentNullException.ThrowIfNull(command);
    command.Options.Add(CommonOptions.From);
    command.Options.Add(CommonOptions.To);
    command.Options.Add(new Option<string>(SourceProfile) { Description = "only pairs of this source profile", HelpName = "id" });
    command.Options.Add(new Option<string>(TargetProfile) { Description = "only pairs of this target profile", HelpName = "id" });
    command.Options.Add(new Option<bool>(All) { Description = "every ordered pair of the report's languages, planned where none has evidence" });
    command.Options.Add(OptionRules.AcceptOnly(new Option<string>(Format) { Description = "the output format", HelpName = "format", DefaultValueFactory = _ => "text" }, "text", "json"));
    command.Options.Add(new Option<string>(Report) { Description = "read this report instead of the embedded one", HelpName = "file" });
  }

  /// <inheritdoc/>
  public int Execute(ParseResult parse, CliContext context) {
    ArgumentNullException.ThrowIfNull(parse);
    ArgumentNullException.ThrowIfNull(context);
    var options = parse.CommandResult.Command.Options;
    string? Value(string name) => parse.GetValue((Option<string>)options.First(option => option.Name == name));
    var file = Value(Report);
    CapabilityReport report;
    if (file is null) {
      report = EmbeddedCapabilities.Load();
    } else if (Read(file, context) is { } read) {
      report = read;
    } else {
      return (int)ExitCode.EnvironmentError;
    }

    var (from, to, sourceProfile, targetProfile) = (Value("--from"), Value("--to"), Value(SourceProfile), Value(TargetProfile));
    if (CapabilityQuery.Invalid(report, from, to, sourceProfile, targetProfile) is { } invalid) {
      context.Report(Diagnostic.Create(CliDiagnostics.CommandLine, null, invalid));
      return (int)ExitCode.UsageError;
    }

    var rows = new CapabilityQuery().Select(report, from, to, sourceProfile, targetProfile, parse.GetValue((Option<bool>)options.First(option => option.Name == All)));
    context.Output.Write(Value(Format) == "json" ? Json(report, rows) : Text(rows));
    return (int)ExitCode.Success;
  }

  /// <summary>
  /// The report a file holds: <c>PLY0004</c> for a file that cannot be read or is no report, and
  /// <c>PLY0061</c> for one that claims a state above <c>planned</c> without evidence, which is never shown.
  /// </summary>
  private static CapabilityReport? Read(string file, CliContext context) {
    if (!CliPaths.RequireInput(file, context))
      return null;

    var text = File.ReadAllText(context.Resolve(file), Encoding.UTF8);
    if (Unbacked(text) is { } claim) {
      context.Report(Diagnostic.Create(CapabilityDiagnostics.Unsupported, null, claim));
      return null;
    }

    if (CapabilityReport.Parse(text, out var errors) is { } report)
      return report;

    context.Report(Diagnostic.Create(CliDiagnostics.Unreadable, null, $"'{file}' is no capability report: {string.Join("; ", errors)}"));
    return null;
  }

  /// <summary>The first pair a report's text claims above <c>planned</c> without evidence, in words; <see langword="null"/> when there is none or the text is no JSON.</summary>
  public static string? Unbacked(string text) {
    ArgumentNullException.ThrowIfNull(text);
    JsonNode? root;
    try {
      root = JsonNode.Parse(text);
    } catch (JsonException) {
      return null;
    }

    if (root is not JsonObject { } report || report["pairs"] is not JsonArray pairs)
      return null;

    static string Part(JsonNode? pair, string name) => pair is JsonObject fields && fields[name] is JsonValue value && value.TryGetValue<string>(out var part) ? part : "?";
    foreach (var claim in pairs)
      if (Part(claim, "state") != "planned" && (claim as JsonObject)?["evidence"] is not JsonValue)
        return $"the report claims {Part(claim, "from")}/{Part(claim, "sourceProfile")} -> {Part(claim, "to")}/{Part(claim, "targetProfile")} at '{Part(claim, "state")}' without evidence";

    return null;
  }

  /// <summary>The text table: a header, one line per row with every column padded to its widest cell and two spaces, an empty line and the summary.</summary>
  public static string Text(IReadOnlyList<CapabilityRow> rows) {
    ArgumentNullException.ThrowIfNull(rows);
    var cells = new List<string[]> { new[] { "FROM", "SOURCE-PROFILE", "TO", "TARGET-PROFILE", "STATE", "EVIDENCE" } };
    cells.AddRange(rows.Select(row => new[] { row.From, row.SourceProfile, row.To, row.TargetProfile, row.State, row.Evidence ?? CapabilityQuery.None }));
    var widths = Enumerable.Range(0, 6).Select(column => cells.Max(line => line[column].Length) + 2).ToArray();
    var text = new StringBuilder();
    foreach (var line in cells)
      text.Append(string.Concat(line.Select((cell, column) => cell.PadRight(widths[column]))).TrimEnd()).Append('\n');

    text.Append('\n').Append(rows.Count(row => row.Evidence is not null)).Append(" pair(s) with evidence; every other pair is planned\n");
    return text.ToString();
  }

  /// <summary>The JSON object: the report's commit and the rows, <c>evidence</c> <see langword="null"/> for a planned one.</summary>
  public static string Json(CapabilityReport report, IReadOnlyList<CapabilityRow> rows) {
    ArgumentNullException.ThrowIfNull(report);
    ArgumentNullException.ThrowIfNull(rows);
    var pairs = new JsonArray([.. rows.Select(row => (JsonNode)new JsonObject {
      ["from"] = row.From, ["sourceProfile"] = row.SourceProfile, ["to"] = row.To, ["targetProfile"] = row.TargetProfile, ["state"] = row.State, ["evidence"] = row.Evidence
    })]);
    return new JsonObject { ["reportCommit"] = report.Commit, ["pairs"] = pairs }.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n";
  }

}
