# Commodore BASIC PRG loader fixtures

Written by hand from the layout of the *Commodore 64 Programmer's Reference Guide* ("BASIC program
storage"): the load address `$0801` (little-endian), then lines of link, line number, tokens and `00`,
and the end marker `00 00` (#1005). `petcat` of the `vice` toolchain lists the valid ones with the same
line numbers in the same order (`PrgLoaderOracleTests`).

| File | Bytes | Program |
| --- | --- | --- |
| `hello.prg` | `01 08 0C 08 0A 00 99 20 22 48 49 22 00 00 00` | `10 PRINT "HI"` |
| `two-lines.prg` | `01 08 0B 08 0A 00 99 22 48 49 22 00 11 08 14 00 80 00 00 00` | `10 PRINT"HI"`, `20 END` |
| `stale-links.prg` | `01 08 FF FF 0A 00 99 00 00 00` | `10 PRINT` with the link `$FFFF` (the next line is at `$0807`) |
| `with-trailer.prg` | `01 08 0B 08 0A 00 9E 32 30 36 31 00 00 00 A9 00 60` | `10 SYS2061`, then `LDA #0`, `RTS` at `$080D` |
| `unterminated.prg` | `01 08 0C 08 0A 00 99 20 22 48 49` | line 10 without its `00` |
| `no-end.prg` | `01 08 07 08 0A 00 99 00` | `10 PRINT` without the end marker |
| `descending.prg` | `01 08 07 08 14 00 80 00 0D 08 0A 00 80 00 00 00` | `20 END`, then `10 END` |

Tokens: `$80` END, `$99` PRINT, `$9E` SYS (`spec/binary/c64-basic-v2/tokens.yaml`).
