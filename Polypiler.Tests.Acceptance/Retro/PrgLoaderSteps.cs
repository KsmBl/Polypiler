using System;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Polypiler.Core;
using Polypiler.Core.BinaryFormats.CbmBasic;
using Polypiler.Prototype.Testing;
using Polypiler.Tests.Acceptance.Support;
using Reqnroll;

namespace Polypiler.Tests.Acceptance.Retro;

[Binding, Scope(Feature = "Commodore BASIC PRG loader")]
public sealed class PrgLoaderSteps {

  private string _name = "input.prg";
  private byte[] _bytes = [];
  private CbmBasicProgram? _program;
  private readonly DiagnosticBag _sink = new();

  private static byte[] Hex(string text) => Convert.FromHexString(text.Replace(" ", "", StringComparison.Ordinal));

  private static int Number(string text) => int.Parse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);

  [Given("^the PRG file " + StepText.Quoted + " with the bytes " + StepText.Quoted + "$")]
  public void File(string name, string bytes) {
    this._name = name;
    this._bytes = Hex(bytes);
    Assert.That(System.IO.File.ReadAllBytes(Path.Combine(PrototypeFixture.RepositoryRoot, "tests", "fixtures", "retro", "cbmbasic", "load", name)), Is.EqualTo(this._bytes), "the fixture file holds the bytes");
  }

  [Given("^a PRG file with the bytes " + StepText.Quoted + " and no dialect given$")]
  public void Bytes(string bytes) => this._bytes = Hex(bytes);

  [When("^the PRG is loaded$")]
  public void Loaded() => this._program = CbmBasicPrgLoader.Load(this._bytes, this._name, null, this._sink);

  [Then("^the dialect is " + StepText.Quoted + " and the load address is (0x[0-9A-F]+)$")]
  public void Dialect(string dialect, string address) => Assert.That((this._program!.Dialect, (int)this._program.LoadAddress), Is.EqualTo((dialect, Number(address))));

  [Then("^there is exactly one line with number (\\d+), address (0x[0-9A-F]+), stored link (0x[0-9A-F]+) and content " + StepText.Quoted + "$")]
  public void Line(int number, string address, string link, string content) {
    var line = this._program!.Lines.Single();
    Assert.That(((int)line.Number, (int)line.Address, (int)line.StoredLink), Is.EqualTo((number, Number(address), Number(link))));
    Assert.That(line.Content.ToArray(), Is.EqualTo(Hex(content)));
  }

  [Then("^the trailer is empty$")]
  public void NoTrailer() => Assert.That(this._program!.Trailer.Length, Is.Zero);

  [Then(@"^there is one line with number (\d+)$")]
  public void OneLine(int number) => Assert.That(this._program!.Lines.Single().Number, Is.EqualTo(number));

  [Then("^the trailer is " + StepText.Quoted + " at address (0x[0-9A-F]+)$")]
  public void Trailer(string bytes, string address)
    => Assert.That((this._program!.Trailer.ToArray(), (int)this._program.TrailerAddress), Is.EqualTo((Hex(bytes), Number(address))));

  [Then("^there is one line with number (\\d+) and content " + StepText.Quoted + "$")]
  public void LineContent(int number, string content) {
    var line = this._program!.Lines.Single();
    Assert.That((int)line.Number, Is.EqualTo(number));
    Assert.That(line.Content.ToArray(), Is.EqualTo(Hex(content)));
  }

  [Then("^the diagnostic (PLY\\d{4}) is reported as a warning with " + StepText.Quoted + "$")]
  public void Warning(string code, string text) {
    var diagnostic = this._sink.Items.Single(item => item.Code == code);
    Assert.That(diagnostic.Severity, Is.EqualTo(DiagnosticSeverity.Warning));
    Assert.That(diagnostic.Message, Does.Contain(StepText.Unescape(text)));
  }

  [Then("^the diagnostic (PLY\\d{4}) is reported with " + StepText.Quoted + "$")]
  public void Reported(string code, string text)
    => Assert.That(this._sink.Items.Single(item => item.Code == code).Message, Does.Contain(StepText.Unescape(text)));

  [Then("^no program is returned$")]
  public void None() => Assert.That(this._program, Is.Null);

}
