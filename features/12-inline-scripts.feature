@P12 @A02 @A13 @A16 @A19
Feature: Inline process scripts and platform variants
  @A14
  Scenario: Execute a frozen multiline PowerShell script through stdin
    Given an inline PowerShell workflow with Unicode output and workspace effects
    When I start the inline workflow and replace its source
    Then start has executed nothing
    When I advance the inline workflow
    Then the frozen script runs once with separate logs and no script file

  @A01 @A05
  Scenario Outline: Reject conflicting or malformed execution definitions before start
    Given an inline workflow with defect <defect>
    When I validate and start the invalid inline workflow
    Then both operations reject the document with a location and no state or children
    Examples:
      | defect                 |
      | script-and-command     |
      | script-and-args        |
      | variants-and-command   |
      | empty-variants         |
      | missing-execution      |
      | missing-shebang        |
      | empty-shebang          |
      | unclosed-quote         |
      | folded-script          |
      | quoted-script          |
      | empty-body             |
      | env-options            |
      | env-assignment         |
      | env-arguments-without-S |
      | unknown-platform       |
      | empty-platforms        |
      | null-platforms         |
      | duplicate-platform     |
      | overlapping-variants   |
      | unconditional-overlap  |
      | equivalent-conditions  |
      | unknown-variant-field  |

  Scenario Outline: Select only the applicable execution variant
    Given a process with <selection> variants
    When I start the inline workflow and replace its source
    And I advance the inline workflow
    Then only the applicable variant executes
    Examples:
      | selection       |
      | single-platform |
      | multi-platform  |
      | unconditional   |
      | direct          |

  Scenario Outline: Route unavailable execution without a substitute interpreter
    Given an inline execution with <unavailable>
    When I start the inline workflow and replace its source
    And I advance the unavailable inline workflow
    Then spawn_error includes the stable execution diagnostic
    Examples:
      | unavailable         |
      | missing-interpreter |
      | unlaunchable-path   |
      | no-matching-variant |

  Scenario Outline: Pass shebang arguments and Unicode body literally to an arbitrary interpreter
    Given an inline probe using <shebang> syntax
    When I start the inline workflow and replace its source
    And I advance the inline workflow
    Then the probe observes exact arguments and UTF-8 stdin without a script file
    Examples:
      | shebang       |
      | absolute      |
      | relative      |
      | quoted-spaces |
      | env           |
      | env-S         |
      | crlf          |

  Scenario Outline: Execute installed stdin-capable interpreters without implicit invocation flags
    Given an inline script for installed <interpreter>
    When I start the inline workflow and replace its source
    And I advance the inline workflow
    Then the interpreter produces Unicode logs and exit_7
    Examples:
      | interpreter |
      | Python      |
      | Bash        |

  @A12
  Scenario Outline: Bound inline interpreter execution and its input feeder
    Given an inline interpreter with <limit> behavior
    When I start the inline workflow and replace its source
    And I advance the inline workflow
    Then inline execution obeys the <limit> limit
    Examples:
      | limit       |
      | timeout     |
      | output      |
      | unread-stdin |

  Scenario: Preserve early interpreter exit while script stdin is still being written
    Given an inline probe that exits before reading a large body
    When I start the inline workflow and replace its source
    And I advance the inline workflow
    Then inline execution preserves exit_7

  Scenario: Fail clearly when no platform variant has a matching outcome route
    Given an uncovered platform without a spawn_error edge
    When I start the inline workflow and replace its source
    And I advance the unavailable inline workflow
    Then advance fails with NO_MATCHING_EDGE and exit 5

  @A10 @A11
  Scenario: Recover an uncertain inline activation only after explicit retry
    Given an inline recovery fixture
    And an interrupted process activation
    When I resolve with retry
    Then the run is ready and no process has started
    When I advance
    Then a new activation ID is used
    And the previous activation remains recorded as interrupted

  @A10 @A11
  Scenario: Interrupt an inline interpreter and its attached process tree
    Given an inline recovery fixture
    And an executing process with an attached child
    When I send the platform's supported user-interrupt signal
    Then the CLI exits with code 130
    And the process tree is terminated on the supported test platform
    And a fresh status reports interrupted
