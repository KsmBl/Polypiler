using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Polypiler.Core.Verification.Capabilities;
using Polypiler.Core.Verification.Gates;

namespace Polypiler.Cli.Capabilities;

/// <summary>One row the <c>capabilities</c> command shows: a pair of a source and a target with the report's state.</summary>
/// <param name="From">The source language.</param>
/// <param name="SourceProfile">The source profile, or <c>-</c> for a planned pair no profile was given for.</param>
/// <param name="To">The target language.</param>
/// <param name="TargetProfile">The target profile, or <c>-</c>.</param>
/// <param name="State">The state as the report names it: <c>planned</c> or one of the ten states of PRD §23.6.</param>
/// <param name="Evidence">The gate record the state rests on, or <see langword="null"/> for <c>planned</c>.</param>
public sealed record CapabilityRow(string From, string SourceProfile, string To, string TargetProfile, string State, string? Evidence);

/// <summary>
/// Selects the rows of a capability report: the pairs with evidence that match every filter, a
/// <c>planned</c> row for a pair of languages that has none, and with <c>--all</c> every ordered pair
/// of the report's languages. It shows the report's state and never computes or upgrades one.
/// </summary>
public sealed class CapabilityQuery {

  /// <summary>The placeholder of a profile or an evidence a planned row has none of.</summary>
  public const string None = "-";

  /// <summary>The report's languages: every source and target, ordinally sorted.</summary>
  /// <param name="report">The report.</param>
  public static IReadOnlyList<string> Languages(CapabilityReport report) {
    ArgumentNullException.ThrowIfNull(report);
    return [.. report.Pairs.SelectMany(pair => new[] { pair.From, pair.To }).Distinct().Order(StringComparer.Ordinal)];
  }

  /// <summary>
  /// The text of the <c>PLY0003</c> of the first filter whose value the report does not know: a language
  /// that is none of the report's, or a profile the report does not list for that language (for every
  /// language when the language is not given); <see langword="null"/> when every filter is valid.
  /// </summary>
  /// <param name="report">The report.</param>
  /// <param name="from">The <c>--from</c> value, or <see langword="null"/>.</param>
  /// <param name="to">The <c>--to</c> value, or <see langword="null"/>.</param>
  /// <param name="sourceProfile">The <c>--source-profile</c> value, or <see langword="null"/>.</param>
  /// <param name="targetProfile">The <c>--target-profile</c> value, or <see langword="null"/>.</param>
  public static string? Invalid(CapabilityReport report, string? from, string? to, string? sourceProfile, string? targetProfile) {
    ArgumentNullException.ThrowIfNull(report);
    var languages = Languages(report);
    string? Check(string option, string? value, IEnumerable<string> allowed) {
      var values = allowed.Distinct().Order(StringComparer.Ordinal).ToList();
      return value is null || values.Contains(value, StringComparer.Ordinal) ? null
        : string.Format(CultureInfo.InvariantCulture, CliDiagnostics.NotAllowed, option, value, string.Join(", ", values));
    }

    return Check("--from", from, languages)
      ?? Check("--to", to, languages)
      ?? Check("--source-profile", sourceProfile, report.Pairs.Where(pair => from is null || pair.From == from).Select(pair => pair.SourceProfile))
      ?? Check("--target-profile", targetProfile, report.Pairs.Where(pair => to is null || pair.To == to).Select(pair => pair.TargetProfile));
  }

  /// <summary>The rows, sorted by source, target, source profile and target profile, ordinally.</summary>
  /// <param name="report">The report.</param>
  /// <param name="from">The source language, or <see langword="null"/> for any.</param>
  /// <param name="to">The target language, or <see langword="null"/> for any.</param>
  /// <param name="sourceProfile">The source profile, or <see langword="null"/> for any.</param>
  /// <param name="targetProfile">The target profile, or <see langword="null"/> for any.</param>
  /// <param name="all">Whether every ordered pair of the report's languages is listed, <c>planned</c> where none has evidence.</param>
  /// <returns>The rows.</returns>
  public IReadOnlyList<CapabilityRow> Select(CapabilityReport report, string? from, string? to, string? sourceProfile, string? targetProfile, bool all) {
    ArgumentNullException.ThrowIfNull(report);
    bool Matches(CapabilityPair pair) => (from is null || pair.From == from) && (to is null || pair.To == to)
      && (sourceProfile is null || pair.SourceProfile == sourceProfile) && (targetProfile is null || pair.TargetProfile == targetProfile);
    CapabilityRow Planned(string source, string target) => new(source, sourceProfile ?? None, target, targetProfile ?? None, GateStates.Name(GateState.Planned), null);

    var rows = report.Pairs.Where(pair => pair.Evidence is not null && Matches(pair))
      .Select(pair => new CapabilityRow(pair.From, pair.SourceProfile, pair.To, pair.TargetProfile, GateStates.Name(pair.State), pair.Evidence)).ToList();
    if (all) {
      var languages = Languages(report);
      foreach (var source in languages.Where(language => from is null || language == from))
        foreach (var target in languages.Where(language => to is null || language == to))
          if (!rows.Any(row => row.From == source && row.To == target))
            rows.Add(Planned(source, target));
    } else if (from is not null && to is not null && rows.Count == 0) {
      rows.Add(Planned(from, to));
    }

    return [.. rows.OrderBy(row => row.From, StringComparer.Ordinal).ThenBy(row => row.To, StringComparer.Ordinal)
      .ThenBy(row => row.SourceProfile, StringComparer.Ordinal).ThenBy(row => row.TargetProfile, StringComparer.Ordinal)];
  }

}
