using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Polypiler.Core;
using System.Collections.Immutable;
using Polypiler.Core.BinaryFormats.CbmBasic;
using Polypiler.Prototype.Testing;
using Polypiler.Tooling.Fuzz;

namespace Polypiler.Tests.Retro.CbmBasic;

/// <summary>
/// The Commodore BASIC PRG loader (#1005): the fixtures of <c>tests/fixtures/retro/cbmbasic/load</c>
/// and every row of the test design, the fuzz case among them.
/// </summary>
[TestFixture]
public sealed class PrgLoaderTests {

  private static readonly string _fixtures = Path.Combine(PrototypeFixture.RepositoryRoot, "tests", "fixtures", "retro", "cbmbasic", "load");

  private static byte[] Bytes(string hex) => Convert.FromHexString(hex.Replace(" ", "", StringComparison.Ordinal));

  private static (CbmBasicProgram? Program, DiagnosticBag Sink) Load(byte[] prg, string? dialect = null, string name = "test.prg") {
    var sink = new DiagnosticBag();
    return (CbmBasicPrgLoader.Load(prg, name, dialect, sink), sink);
  }

  private static (CbmBasicProgram? Program, DiagnosticBag Sink) Fixture(string name) => Load(File.ReadAllBytes(Path.Combine(_fixtures, name)), null, name);

  [Test]
  public void AOneLineProgram_IsSplitIntoHeaderLineAndEndMarker() {
    var (program, sink) = Fixture("hello.prg");

    Assert.That(sink.Items, Is.Empty);
    Assert.That((program!.Dialect, program.LoadAddress), Is.EqualTo(("c64-basic-v2", (ushort)0x0801)));
    var line = program.Lines.Single();
    Assert.That((line.Number, line.Address, line.StoredLink), Is.EqualTo(((ushort)10, (ushort)0x0801, (ushort)0x080C)));
    Assert.That(line.Content.ToArray(), Is.EqualTo(Bytes("99 20 22 48 49 22")));
    Assert.That((program.Trailer.Length, program.TrailerAddress), Is.EqualTo((0, (ushort)0x080E)));
  }

  [Test]
  public void SeveralLines_AreReadInOrderWithConsistentLinks() {
    var (program, sink) = Fixture("two-lines.prg");

    Assert.That(sink.Items, Is.Empty);
    Assert.That(program!.Lines.Select(line => (line.Number, line.Address, line.StoredLink)),
      Is.EqualTo(new[] { ((ushort)10, (ushort)0x0801, (ushort)0x080B), ((ushort)20, (ushort)0x080B, (ushort)0x0811) }));
  }

  [Test]
  public void BytesAfterTheEndMarker_AreTheTrailer() {
    var (program, _) = Fixture("with-trailer.prg");

    Assert.That(program!.Lines.Single().Number, Is.EqualTo(10));
    Assert.That((program.Trailer.ToArray(), program.TrailerAddress), Is.EqualTo((Bytes("A9 00 60"), (ushort)0x080D)));
  }

  [Test]
  public void AStaleLink_IsAWarning_AndTheProgramIsStillRead() {
    var (program, sink) = Fixture("stale-links.prg");
    var warning = sink.Items.Single();

    Assert.That(program!.Lines.Single().Content.ToArray(), Is.EqualTo(Bytes("99")));
    Assert.That((warning.Code, warning.Severity, warning.Message), Is.EqualTo(("PLY3902", DiagnosticSeverity.Warning, "line 10: stored link $FFFF differs from the next line's address $0807")));
    Assert.That((warning.File, warning.Line, warning.Column), Is.EqualTo(("stale-links.prg", 1, 3)));
  }

  [Test]
  public void AForeignLoadAddress_WithAnExplicitDialect_IsChecked_AgainstThatAddress() {
    var (program, sink) = Load(Bytes("01 10 0C 10 0A 00 99 20 22 48 49 22 00 00 00"), "c64-basic-v2");

    Assert.That(sink.Items, Is.Empty);
    Assert.That((program!.LoadAddress, program.Lines.Single().Address), Is.EqualTo(((ushort)0x1001, (ushort)0x1001)));
  }

  [Test]
  public void DescendingAndRepeatedLineNumbers_AreWarnings() {
    var (descending, warnings) = Fixture("descending.prg");
    var (_, repeated) = Load(Bytes("01 08 07 08 0A 00 80 00 0D 08 0A 00 80 00 00 00"));

    Assert.That(descending!.Lines.Select(line => line.Number), Is.EqualTo(new ushort[] { 20, 10 }));
    Assert.That(warnings.Items.Single().Message, Is.EqualTo("line number 10 follows 20"));
    Assert.That(repeated.Items.Single().Message, Is.EqualTo("line number 10 follows 10"));
  }

  [Test]
  public void AProgramWithoutLines_IsAccepted() {
    var (program, sink) = Load(Bytes("01 08 00 00"));

    Assert.That((program!.Lines.Length, sink.Items.Count), Is.EqualTo((0, 0)));
  }

  [Test]
  public void ALineWithoutContent_IsALine() {
    var (program, sink) = Load(Bytes("01 08 06 08 0A 00 00 00 00"));

    Assert.That(sink.Items, Is.Empty);
    Assert.That(program!.Lines.Single().Content.Length, Is.Zero);
  }

  [TestCase(0, false)]
  [TestCase(63999, false)]
  [TestCase(64000, true)]
  [TestCase(65535, true)]
  public void ALineNumberAbove63999_IsAWarning(int number, bool warned) {
    var (program, sink) = Load(Bytes($"01 08 06 08 {number & 0xFF:X2} {number >> 8:X2} 00 00 00"));

    Assert.That(program!.Lines.Single().Number, Is.EqualTo(number));
    Assert.That(sink.Items.Select(diagnostic => diagnostic.Message), warned ? Is.EqualTo(new[] { $"line number {number} exceeds 63999" }) : Is.Empty);
  }

  [Test]
  public void AFileOfThreeBytes_IsTooShort() {
    var (program, sink) = Load(Bytes("01 08 00"), null, "short.prg");

    Assert.That(program, Is.Null);
    Assert.That((sink.Items.Single().Code, sink.Items.Single().Message), Is.EqualTo(("PLY3900", "'short.prg' is too short to be a Commodore BASIC program (3 byte(s))")));
  }

  [TestCase("01 08 0C 08 0A", 5)]
  [TestCase("01 08 07 08 0A 00 99 00 00", 9)]
  public void TheEndOfFileInsideALinkOrALineNumber_IsAMissingEndMarker(string hex, int length) {
    var (program, sink) = Load(Bytes(hex));

    Assert.That(program, Is.Null);
    Assert.That((sink.Items.Single().Code, sink.Items.Single().Message), Is.EqualTo(("PLY3905", $"program ends at offset {length} without an end marker")));
  }

  [Test]
  public void AnUnterminatedLine_IsRefused() {
    var (program, sink) = Fixture("unterminated.prg");

    Assert.That(program, Is.Null);
    Assert.That((sink.Items.Single().Code, sink.Items.Single().Message), Is.EqualTo(("PLY3903", "line 10 at offset 2 is not terminated")));
  }

  [Test]
  public void AMissingEndMarker_IsRefused() {
    var (program, sink) = Fixture("no-end.prg");

    Assert.That(program, Is.Null);
    Assert.That(sink.Items.Single().Message, Is.EqualTo("program ends at offset 8 without an end marker"));
  }

  [Test]
  public void MachineCodeWithAPrgHeader_IsNotMistakenForBasic() {
    var (program, sink) = Load(Bytes("00 C0 A9 00 60"));

    Assert.That(program, Is.Null);
    Assert.That((sink.Items.Single().Code, sink.Items.Single().Message), Is.EqualTo(("PLY3901", "load address $C000 is not the BASIC start of a known dialect; pass a dialect explicitly")));
  }

  [Test]
  public void ADialectRegistersItsLoadAddress() {
    CbmBasicDialects.RegisterLoadAddress(0x1201, "test-dialect-1201");
    CbmBasicDialects.RegisterLoadAddress(0x1201, "test-dialect-1201");

    Assert.That(CbmBasicDialects.ByLoadAddress(0x1201), Is.EqualTo("test-dialect-1201"));
    Assert.That(CbmBasicDialects.ByLoadAddress(0x0801), Is.EqualTo(CbmBasicDialects.C64BasicV2));
    Assert.That(CbmBasicDialects.ByLoadAddress(0xC000), Is.Null);
    Assert.Throws<InvalidOperationException>(() => CbmBasicDialects.RegisterLoadAddress(0x0801, "other"));
    Assert.Throws<ArgumentException>(() => CbmBasicDialects.RegisterLoadAddress(0x2000, " "));
  }

  [Test]
  public void ProgramsAndLines_CompareByContent() {
    var (first, _) = Fixture("two-lines.prg");
    var (second, _) = Fixture("two-lines.prg");

    Assert.That(first, Is.EqualTo(second));
    Assert.That(first!.GetHashCode(), Is.EqualTo(second!.GetHashCode()));
    Assert.That(first.Lines[0].GetHashCode(), Is.EqualTo(second.Lines[0].GetHashCode()));
    Assert.That(first, Is.Not.EqualTo(Fixture("hello.prg").Program));
    Assert.That(first.Equals(null), Is.False);
    Assert.That(first.Lines[0].Equals(null), Is.False);
  }

  [Test]
  public void NullArguments_AreRefused() {
    Assert.Throws<ArgumentNullException>(() => CbmBasicPrgLoader.Load(Bytes("01 08 00 00"), null!, null, new DiagnosticBag()));
    Assert.Throws<ArgumentNullException>(() => CbmBasicPrgLoader.Load(Bytes("01 08 00 00"), "a", null, null!));
  }

  /// <summary>Test design 15: 10 000 mutations of the fixtures (seed 3900) give a program or a diagnostic, never an exception, and no line holds a <c>00</c>.</summary>
  [Test]
  public void TenThousandMutatedFixtures_GiveAProgramOrADiagnostic() {
    var target = new CbmBasicLoadFuzzTarget();
    var seeds = Directory.GetFiles(_fixtures, "*.prg").Order(StringComparer.Ordinal).Select(File.ReadAllBytes).ToArray();
    var random = new Random(3900);
    for (var iteration = 0; iteration < 10_000; ++iteration) {
      var bytes = (byte[])seeds[random.Next(seeds.Length)].Clone();
      for (var mutation = random.Next(1, 4); mutation > 0 && bytes.Length > 0; --mutation)
        bytes[random.Next(bytes.Length)] = (byte)random.Next(256);
      if (random.Next(4) == 0)
        bytes = bytes[..random.Next(bytes.Length + 1)];
      Assert.DoesNotThrow(() => target.Execute(bytes), Convert.ToHexString(bytes));
    }

    Assert.That(target.Id, Is.EqualTo("cbmbasic-load"));
  }

  [Test]
  public void TheFuzzCheck_FailsAResultThatBreaksTheContract() {
    var broken = new CbmBasicProgram("c64-basic-v2", 0x0801, [new CbmBasicLine(10, new byte[] { 0x99, 0x00 }, 0x0801, 0x0808)], default, 0x0808);

    Assert.Throws<InvalidOperationException>(() => CbmBasicLoadFuzzTarget.Check(null, []));
    Assert.Throws<InvalidOperationException>(() => CbmBasicLoadFuzzTarget.Check(broken, []));
    Assert.DoesNotThrow(() => CbmBasicLoadFuzzTarget.Check(broken with { Lines = [] }, []));
    Assert.Throws<ArgumentNullException>(() => CbmBasicLoadFuzzTarget.Check(null, null!));
  }

}
