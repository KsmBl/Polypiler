using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;

namespace Polypiler.Core.BinaryFormats.CbmBasic;

/// <summary>The diagnostics of the PRG loader, as <c>spec/diagnostics.yaml</c> registers them.</summary>
public static class CbmBasicDiagnostics {

  /// <summary><c>PLY3900</c>: fewer than four bytes. Arguments: the file name, the length.</summary>
  [Diagnostic]
  public static readonly DiagnosticDescriptor TooShort = new("PLY3900", DiagnosticSeverity.Error, ExitCode.InvalidSource,
    "'{0}' is too short to be a Commodore BASIC program ({1} byte(s))", "tokenized-basic.load.short");

  /// <summary><c>PLY3901</c>: a load address no dialect registered, and no dialect given. Argument: the address in hex.</summary>
  [Diagnostic]
  public static readonly DiagnosticDescriptor UnknownLoadAddress = new("PLY3901", DiagnosticSeverity.Error, ExitCode.InvalidSource,
    "load address ${0} is not the BASIC start of a known dialect; pass a dialect explicitly", "tokenized-basic.load.address");

  /// <summary><c>PLY3902</c>: a stored link that is not the next line's address. Arguments: the line, the link, the address, both in hex.</summary>
  [Diagnostic]
  public static readonly DiagnosticDescriptor StaleLink = new("PLY3902", DiagnosticSeverity.Warning, null,
    "line {0}: stored link ${1} differs from the next line's address ${2}", "tokenized-basic.load.link");

  /// <summary><c>PLY3903</c>: line content running to the end of the file. Arguments: the line, its offset.</summary>
  [Diagnostic]
  public static readonly DiagnosticDescriptor Unterminated = new("PLY3903", DiagnosticSeverity.Error, ExitCode.InvalidSource,
    "line {0} at offset {1} is not terminated", "tokenized-basic.load.unterminated");

  /// <summary><c>PLY3904</c>: a line number not above its predecessor, or above 63999. Argument: the reason.</summary>
  [Diagnostic]
  public static readonly DiagnosticDescriptor LineNumber = new("PLY3904", DiagnosticSeverity.Warning, null,
    "{0}", "tokenized-basic.load.number");

  /// <summary><c>PLY3905</c>: the file ends where a link or a line number is expected. Argument: the file's length.</summary>
  [Diagnostic]
  public static readonly DiagnosticDescriptor NoEndMarker = new("PLY3905", DiagnosticSeverity.Error, ExitCode.InvalidSource,
    "program ends at offset {0} without an end marker", "tokenized-basic.load.end");

}

/// <summary>
/// Splits a Commodore BASIC PRG into its lines (<i>Commodore 64 Programmer's Reference Guide</i>, "BASIC
/// program storage"): the load address (two bytes, little-endian), then lines of a link (the address
/// of the next line), a line number, content bytes none of which is <c>00</c>, and a <c>00</c>; a link
/// <c>00 00</c> ends the program, and whatever follows is the trailer. The walk follows the
/// terminators, as the machine's relink at <c>$A533</c> does (Leemon, <i>Mapping the Commodore 64</i>),
/// so a stale link is a warning, not a different program. Content bytes are returned untouched and
/// nothing is repaired; warnings never change the result. A diagnostic's column is its byte offset
/// plus one, on line 1.
/// </summary>
public static class CbmBasicPrgLoader {

  /// <summary>The highest line number BASIC V2 accepts when a line is typed.</summary>
  public const ushort MaxLineNumber = 63999;

  /// <summary>Loads a PRG.</summary>
  /// <param name="prg">The file's bytes.</param>
  /// <param name="name">The file's name, for the diagnostics.</param>
  /// <param name="dialect">The dialect; <see langword="null"/> to take it from the load address.</param>
  /// <param name="sink">Receives the warnings and errors.</param>
  /// <returns>The program, or <see langword="null"/> after an error.</returns>
  public static CbmBasicProgram? Load(ReadOnlyMemory<byte> prg, string name, string? dialect, IDiagnosticSink sink) {
    ArgumentNullException.ThrowIfNull(name);
    ArgumentNullException.ThrowIfNull(sink);
    var bytes = prg.Span;
    void Report(DiagnosticDescriptor descriptor, int offset, params object[] arguments)
      => sink.Report(Diagnostic.Create(descriptor, new SourceRange(name, 1, offset + 1, 1, offset + 1), arguments));

    if (bytes.Length < 4) {
      Report(CbmBasicDiagnostics.TooShort, 0, name, bytes.Length);
      return null;
    }

    var load = BinaryPrimitives.ReadUInt16LittleEndian(bytes);
    var chosen = dialect ?? CbmBasicDialects.ByLoadAddress(load);
    if (chosen is null) {
      Report(CbmBasicDiagnostics.UnknownLoadAddress, 0, Hex(load));
      return null;
    }

    var lines = ImmutableArray.CreateBuilder<CbmBasicLine>();
    var offset = 2;
    var address = load;
    while (true) {
      if (offset + 2 > bytes.Length) {
        Report(CbmBasicDiagnostics.NoEndMarker, bytes.Length, bytes.Length);
        return null;
      }

      var link = BinaryPrimitives.ReadUInt16LittleEndian(bytes[offset..]);
      if (link == 0)
        return new(chosen, load, lines.ToImmutable(), prg[(offset + 2)..], unchecked((ushort)(address + 2)));

      if (offset + 4 > bytes.Length) {
        Report(CbmBasicDiagnostics.NoEndMarker, bytes.Length, bytes.Length);
        return null;
      }

      var number = BinaryPrimitives.ReadUInt16LittleEndian(bytes[(offset + 2)..]);
      var terminator = bytes[(offset + 4)..].IndexOf((byte)0);
      if (terminator < 0) {
        Report(CbmBasicDiagnostics.Unterminated, offset, number, offset);
        return null;
      }

      var end = offset + 4 + terminator;
      var next = unchecked((ushort)(address + (end + 1 - offset)));
      if (link != next)
        Report(CbmBasicDiagnostics.StaleLink, offset, number, Hex(link), Hex(next));
      if (lines.Count > 0 && number <= lines[^1].Number)
        Report(CbmBasicDiagnostics.LineNumber, offset + 2, $"line number {number} follows {lines[^1].Number}");
      if (number > MaxLineNumber)
        Report(CbmBasicDiagnostics.LineNumber, offset + 2, $"line number {number} exceeds {MaxLineNumber}");

      lines.Add(new(number, prg[(offset + 4)..end], address, link));
      offset = end + 1;
      address = next;
    }
  }

  private static string Hex(ushort value) => value.ToString("X4", CultureInfo.InvariantCulture);

}
