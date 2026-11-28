@Layer.core
Feature: polypiler capabilities

  The fixtures of tests/fixtures/cli/capabilities are copied into the workspace under the same path.

  Scenario: A verified pair is reported with its evidence
    When I run "polypiler capabilities --from csharp --to javascript --report tests/fixtures/cli/capabilities/two-pairs.capabilities.json"
    Then the exit code is 0
    And stdout has exactly one data row with the cells "csharp", "net10", "javascript", "es2022", "verified"
    And that row's evidence cell is not "-"

  Scenario: The reverse direction keeps its own, lower state
    When I run "polypiler capabilities --from javascript --to csharp --report tests/fixtures/cli/capabilities/two-pairs.capabilities.json"
    Then the exit code is 0
    And stdout has exactly one data row with state "emitted"

  Scenario: A pair without evidence is planned
    When I run "polypiler capabilities --from kotlin --to perl --source-profile jvm --target-profile perl5 --report tests/fixtures/cli/capabilities/empty.capabilities.json"
    Then the exit code is 0
    And stdout has exactly one data row with the cells "kotlin", "jvm", "perl", "perl5", "planned", "-"
    And the final line of stdout is "0 pair(s) with evidence; every other pair is planned"

  Scenario: Listing without filters shows only evidenced pairs
    When I run "polypiler capabilities --report tests/fixtures/cli/capabilities/two-pairs.capabilities.json"
    Then the exit code is 0
    And stdout has exactly 2 data rows, "csharp" first
    And the final line of stdout is "2 pair(s) with evidence; every other pair is planned"

  Scenario: An unknown language is refused
    When I run "polypiler capabilities --from klingon --report tests/fixtures/cli/capabilities/two-pairs.capabilities.json"
    Then the exit code is 2
    And stderr contains "error PLY0003: 'klingon' is not a valid value for '--from'"

  Scenario: A report that claims support without evidence is not shown
    When I run "polypiler capabilities --report tests/fixtures/cli/capabilities/unbacked.capabilities.json"
    Then the exit code is 1
    And stderr contains "error PLY0061"
    And stdout is empty
