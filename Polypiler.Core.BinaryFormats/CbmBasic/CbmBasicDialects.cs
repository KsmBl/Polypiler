using System;
using System.Collections.Generic;

namespace Polypiler.Core.BinaryFormats.CbmBasic;

/// <summary>
/// The dialects of Commodore BASIC by the load address of their programs: the BASIC start a machine's
/// ROM loads a program to (<c>$0801</c>, the start of BASIC at <c>$002B</c> on the C64). Other
/// dialects register their addresses here.
/// </summary>
public static class CbmBasicDialects {

  /// <summary>The C64's BASIC V2.</summary>
  public const string C64BasicV2 = "c64-basic-v2";

  private static readonly Dictionary<ushort, string> _byAddress = new() { [0x0801] = C64BasicV2 };

  private static readonly object _lock = new();

  /// <summary>Registers the load address of a dialect.</summary>
  /// <param name="address">The BASIC start of its machine.</param>
  /// <param name="dialect">The dialect.</param>
  /// <exception cref="InvalidOperationException">The address belongs to another dialect already.</exception>
  public static void RegisterLoadAddress(ushort address, string dialect) {
    ArgumentException.ThrowIfNullOrWhiteSpace(dialect);
    lock (_lock) {
      if (_byAddress.TryGetValue(address, out var known) && known != dialect)
        throw new InvalidOperationException($"the load address ${address:X4} belongs to the dialect '{known}' already");

      _byAddress[address] = dialect;
    }
  }

  /// <summary>The dialect whose programs load at an address, or <see langword="null"/> when none is registered.</summary>
  /// <param name="address">The load address.</param>
  public static string? ByLoadAddress(ushort address) {
    lock (_lock)
      return _byAddress.GetValueOrDefault(address);
  }

}
