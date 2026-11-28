using System;
using System.Collections.Immutable;
using System.Linq;

namespace Polypiler.Core.BinaryFormats.CbmBasic;

/// <summary>One line of a Commodore BASIC program as the PRG stores it.</summary>
/// <param name="Number">The line number.</param>
/// <param name="Content">The tokenized bytes between the line number and the terminating <c>00</c>, untouched.</param>
/// <param name="Address">The address of the line's first byte in memory.</param>
/// <param name="StoredLink">The link the file stores, which may be stale.</param>
public sealed record CbmBasicLine(ushort Number, ReadOnlyMemory<byte> Content, ushort Address, ushort StoredLink) {

  /// <inheritdoc/>
  public bool Equals(CbmBasicLine? other)
    => other is not null && this.Number == other.Number && this.Content.Span.SequenceEqual(other.Content.Span) && this.Address == other.Address && this.StoredLink == other.StoredLink;

  /// <inheritdoc/>
  public override int GetHashCode() => HashCode.Combine(this.Number, this.Content.Length, this.Address, this.StoredLink);

}

/// <summary>A Commodore BASIC program split into its lines: the dialect, the load address, the lines in file order and the bytes after the end marker.</summary>
/// <param name="Dialect">The dialect, from the load address or the caller.</param>
/// <param name="LoadAddress">The load address the file's first two bytes state.</param>
/// <param name="Lines">The lines, in file order.</param>
/// <param name="Trailer">The bytes after the end marker, such as machine code; empty when there are none.</param>
/// <param name="TrailerAddress">The address of the trailer's first byte: the one after the end marker.</param>
public sealed record CbmBasicProgram(string Dialect, ushort LoadAddress, ImmutableArray<CbmBasicLine> Lines, ReadOnlyMemory<byte> Trailer, ushort TrailerAddress) {

  /// <inheritdoc/>
  public bool Equals(CbmBasicProgram? other)
    => other is not null && this.Dialect == other.Dialect && this.LoadAddress == other.LoadAddress && this.Lines.SequenceEqual(other.Lines)
      && this.Trailer.Span.SequenceEqual(other.Trailer.Span) && this.TrailerAddress == other.TrailerAddress;

  /// <inheritdoc/>
  public override int GetHashCode() => HashCode.Combine(this.Dialect, this.LoadAddress, this.Lines.Length, this.Trailer.Length);

}
