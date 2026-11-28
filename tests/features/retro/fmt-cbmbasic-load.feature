@Layer.core
Feature: Commodore BASIC PRG loader

  A PRG is a load address and lines of link, line number, tokens and a terminating 00, then the end
  marker 00 00; whatever follows is the trailer.

  Scenario: A one-line program is split into header, line and end marker
    Given the PRG file "hello.prg" with the bytes "01 08 0C 08 0A 00 99 20 22 48 49 22 00 00 00"
    When the PRG is loaded
    Then the dialect is "c64-basic-v2" and the load address is 0x0801
    And there is exactly one line with number 10, address 0x0801, stored link 0x080C and content "99 20 22 48 49 22"
    And the trailer is empty

  Scenario: Bytes after the end marker are kept as a trailer
    Given the PRG file "with-trailer.prg" with the bytes "01 08 0B 08 0A 00 9E 32 30 36 31 00 00 00 A9 00 60"
    When the PRG is loaded
    Then there is one line with number 10
    And the trailer is "A9 00 60" at address 0x080D

  Scenario: A stale link is reported and the program is still read
    Given the PRG file "stale-links.prg" with the bytes "01 08 FF FF 0A 00 99 00 00 00"
    When the PRG is loaded
    Then there is one line with number 10 and content "99"
    And the diagnostic PLY3902 is reported as a warning with "stored link $FFFF differs from the next line's address $0807"

  Scenario: An unterminated line is refused
    Given the PRG file "unterminated.prg" with the bytes "01 08 0C 08 0A 00 99 20 22 48 49"
    When the PRG is loaded
    Then the diagnostic PLY3903 is reported with "line 10 at offset 2 is not terminated"
    And no program is returned

  Scenario: A missing end marker is refused
    Given the PRG file "no-end.prg" with the bytes "01 08 07 08 0A 00 99 00"
    When the PRG is loaded
    Then the diagnostic PLY3905 is reported with "program ends at offset 8 without an end marker"

  Scenario: Machine code with a PRG header is not mistaken for BASIC
    Given a PRG file with the bytes "00 C0 A9 00 60" and no dialect given
    When the PRG is loaded
    Then the diagnostic PLY3901 is reported with "load address $C000 is not the BASIC start of a known dialect"
