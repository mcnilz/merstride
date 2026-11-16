@P13 @A20 @A16
Feature: Readable output by default and explicit stable JSON
  Scenario: Discover the version as text even when stdout is redirected
    Given an isolated readable-output workspace
    When I invoke readable discovery with version
    Then the default response is a readable version with no state or children

  Scenario Outline: Read offline discovery in either output mode
    Given an isolated readable-output workspace
    When I invoke readable discovery with <arguments>
    Then discovery is <format> containing <content> without filesystem effects
    Examples:
      | arguments                                                 | format | content                         |
      | --version                                                 | text   | Merstride                       |
      | version;--state-dir;not-absolute                            | text   | Merstride                       |
      | guide;--state-dir;not-absolute                              | text   | Merstride protocol 1            |
      | creation-guide;--state-dir;not-absolute                      | text   | Merstride workflow creation     |
      | --help                                                    | text   | Usage:                          |
      | creation-guide;--help                                      | text   | Usage:                          |
      | --json;--version                                           | json   | version                         |
      | version;--json                                             | json   | version                         |
      | --json;guide;--state-dir;not-absolute                        | json   | guide                           |
      | creation-guide;--json;--state-dir;not-absolute               | json   | guide                           |
      | --json;--help                                              | json   | guide                           |
      | creation-guide;--help;--json                                | json   | guide                           |

  @A05
  Scenario Outline: Explain errors in the selected output mode
    Given a readable-output fixture with <defect>
    When I invoke its failing command as <format>
    Then the selected output reports <code> with exit <exit>
    Examples:
      | defect          | format | code             | exit |
      | usage           | text   | USAGE            | 2    |
      | usage           | json   | USAGE            | 2    |
      | validation      | text   | INVALID_DOCUMENT | 3    |
      | validation      | json   | INVALID_DOCUMENT | 3    |
      | state           | text   | INVALID_RUN_ID   | 4    |
      | state           | json   | INVALID_RUN_ID   | 4    |
      | io              | text   | IO_ERROR         | 6    |
      | io              | json   | IO_ERROR         | 6    |
      | duplicate-json  | json   | USAGE            | 2    |
      | malformed-json  | json   | USAGE            | 2    |
      | json-after-end  | text   | USAGE            | 2    |
      | json-global     | json   | USAGE            | 2    |

  @A02 @A03 @A04 @A07 @A09
  Scenario: Display an agent handoff and complete it without changing execution semantics
    Given a readable workflow with selected truncated and unavailable context
    When I start the workflow as text
    Then start displays the run ID and state root without executing
    When I advance to the agent as text
    Then the handoff displays literal instructions outcomes and selected context
    When I complete the request as text and repeat it as JSON
    Then the receipt is idempotent and only the cursor moved
    When I advance to the terminal state as text
    Then readable status agrees with JSON and does not replay work

  @A02 @A12
  Scenario Outline: Read bounded logs without rendering terminal controls
    Given a readable log workflow producing <content>
    When I read its logs as text and JSON
    Then log bytes stay unchanged and both modes describe the same bounded tail
    Examples:
      | content |
      | Unicode |
      | raw     |
      | tail    |

  @A11
  Scenario Outline: Resolve interrupted work as text
    Given an isolated readable-output workspace
    When I resolve interrupted work as text with <resolution>
    Then recovery retains its existing status exit and no-replay semantics
    Examples:
      | resolution |
      | retry      |
      | fail       |

  Scenario: Show validation warnings separately from the readable success result
    Given a readable log workflow producing Unicode
    When I validate its unlabeled process edge as text
    Then validation success is on stdout and a located warning is on stderr

  Scenario: Inspect a failed workflow successfully in either mode
    Given a failed readable workflow
    When I inspect the failed workflow as text and JSON
    Then both inspections exit zero and preserve the failure reason
