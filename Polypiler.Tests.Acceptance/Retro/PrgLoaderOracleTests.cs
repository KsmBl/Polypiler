using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Polypiler.Core;
using Polypiler.Core.BinaryFormats.CbmBasic;
using Polypiler.Core.Verification.Emulators;
using Polypiler.Tests.Acceptance.Infra;

namespace Polypiler.Tests.Acceptance.Retro;

/// <summary>
/// The PRG loader against petcat of the <c>vice</c> toolchain (#1005): for every valid fixture of
/// <c>tests/fixtures/retro/cbmbasic/load</c>, the line numbers petcat lists, in its order, are those the
/// loader reads.
/// </summary>
[TestFixture, Category("ExternalInterop"), Layer("basic")]
[RequiresToolchain(Petcat.Toolchain)]
public sealed partial class PrgLoaderOracleTests {

  private static readonly string _fixtures = Path.Combine(Polypiler.Prototype.Testing.PrototypeFixture.RepositoryRoot, "tests", "fixtures", "retro", "cbmbasic", "load");

  [TestCase("hello.prg")]
  [TestCase("two-lines.prg")]
  [TestCase("stale-links.prg")]
  [TestCase("with-trailer.prg")]
  [TestCase("descending.prg")]
  public void TheLineNumbers_AreThoseThatPetcatLists(string name) {
    var prg = File.ReadAllBytes(Path.Combine(_fixtures, name));
    var program = CbmBasicPrgLoader.Load(prg, name, null, new DiagnosticBag())!;
    var listed = CbmTools.Petcat().DetokenizeAsync(prg, CbmBasicDialects.C64BasicV2).GetAwaiter().GetResult();

    Assert.That(listed.Succeeded, Is.True, string.Join("\n", listed.Diagnostics));
    Assert.That(ListedNumber().Matches(listed.Listing).Select(match => int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)),
      Is.EqualTo(program.Lines.Select(line => (int)line.Number)));
  }

  [GeneratedRegex(@"^\s*(\d+)\s", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
  private static partial Regex ListedNumber();

}
