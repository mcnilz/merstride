@P23 @A13
Feature: Optional RTK wrapping of process commands
  Scenario: Wrap a new command by default when RTK becomes available after start
    Given an RTK workflow with the default settings
    When I start the RTK workflow and make RTK available
    And I advance the RTK workflow
    Then RTK runs the command once with its literal arguments

  Scenario Outline: Apply the effective RTK setting to the frozen command
    Given an RTK command with workflow setting <workflow> and node setting <node>
    When I start the RTK workflow and prepare <availability>
    And I advance the RTK workflow expecting <execution>
    Then the command execution is <execution>
    Examples:
      | workflow | node    | availability | execution |
      | omitted  | omitted | absent       | direct    |
      | false    | omitted | available    | direct    |
      | false    | true    | available    | wrapped   |
      | true     | false   | available    | direct    |
      | true     | omitted | available    | wrapped   |
      | omitted  | omitted | unusable     | direct    |
      | omitted  | omitted | quoted-empty | direct    |
      | omitted  | omitted | available    | legacy    |
      | true     | omitted | failing      | failed    |

  Scenario Outline: Wrap command variants and leave inline scripts direct
    Given an RTK workflow with a <form> process
    When I start the RTK workflow and make RTK available
    And I advance the RTK workflow expecting <execution>
    Then the command execution is <execution>
    Examples:
      | form            | execution      |
      | command-variant | wrapped        |
      | script          | script-direct  |
      | script-variant  | script-direct  |

  Scenario Outline: Preserve executable dispatch through RTK
    Given an RTK workflow with a <form> process
    When I start the RTK workflow and make RTK available
    And I advance the RTK workflow expecting wrapped
    Then the command execution is wrapped
    Examples:
      | form            |
      | ambiguous-name  |
      | named-filter    |

  @A01
  Scenario Outline: Reject RTK settings outside the supported YAML schema
    Given an RTK workflow with invalid <field>
    When I validate and start the invalid RTK workflow
    Then both operations reject the RTK document before any process runs
    Examples:
      | field        |
      | root-string  |
      | node-number  |
      | node-null    |
      | end-setting  |
      | variant-flag |
