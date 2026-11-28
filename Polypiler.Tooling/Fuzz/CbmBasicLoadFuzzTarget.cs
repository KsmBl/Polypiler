using System;
using System.Collections.Generic;
using System.Linq;
using Polypiler.Core;
using Polypiler.Core.BinaryFormats.CbmBasic;
using Polypiler.Core.Verification.Fuzzing;

namespace Polypiler.Tooling.Fuzz;

/// <summary>
/// The PRG loader on any bytes (#1005): it gives a program or a diagnostic, never an exception, and no
/// line it returns holds a <c>00</c>, which terminates a line.
/// </summary>
public sealed class CbmBasicLoadFuzzTarget : IFuzzTarget {

  /// <inheritdoc/>
  public string Id => "cbmbasic-load";

  /// <inheritdoc/>
  public void Execute(ReadOnlySpan<byte> input) {
    var sink = new DiagnosticBag();
    Check(CbmBasicPrgLoader.Load(input.ToArray(), "fuzz.prg", null, sink), sink.Items);
  }

  /// <summary>Fails an input whose result is neither a program nor a diagnostic, or a program with a line that holds a <c>00</c>.</summary>
  /// <param name="program">The program, or <see langword="null"/>.</param>
  /// <param name="diagnostics">The diagnostics.</param>
  /// <exception cref="InvalidOperationException">The result breaks the loader's contract.</exception>
  public static void Check(CbmBasicProgram? program, IReadOnlyList<Diagnostic> diagnostics) {
    ArgumentNullException.ThrowIfNull(diagnostics);
    if (program is null && diagnostics.Count == 0)
      throw new InvalidOperationException("the loader returned no program and no diagnostic");
    if (program is not null && program.Lines.Any(line => line.Content.Span.IndexOf((byte)0) >= 0))
      throw new InvalidOperationException("a line holds its terminator");
  }

}
