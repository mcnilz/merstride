@contract @P02
Feature: Execute deterministic process nodes
  Background:
    Given an isolated workspace and state directory
    And a controlled native test process

  @A02
  Scenario: Run a process chain without agent work
    Given a valid workflow with two successful process nodes followed by a success end
    When I start and advance the workflow
    Then both process effects occur once in graph order
    And the run is completed with CLI exit code 0
    And no agent request is returned

  @A02
  Scenario Outline: Route process outcomes deterministically
    Given a process workflow with the outcome routes "exit_0", "exit_7", "spawn_error", and "default" to distinct end nodes
    When its process produces <result>
    Then the route labeled <route> is taken
    Examples:
      | result              | route       |
      | exit code 0         | exit_0      |
      | exit code 7         | exit_7      |
      | exit code 9         | default     |
      | an unavailable executable | spawn_error |

  @A02
  Scenario: Fail when no route matches
    Given a process exiting with code 7 and only an "exit_0" edge
    When I advance the workflow
    Then the run fails with "NO_MATCHING_EDGE"
    And the CLI exits with code 5

  @A13
  Scenario: Pass literal arguments without shell interpretation
    Given a process node with arguments "two words", "Grüße", "$(touch injected)", and "a;b"
    When I advance the workflow
    Then the test process receives those four exact arguments in order
    And no file named "injected" exists

  @A13
  Scenario: Invoke PowerShell explicitly
    Given an installed PowerShell 7 and a process node invoking a workspace script through "pwsh"
    When I advance the workflow
    Then the script runs with the selected workspace as its working directory
    And its exit code determines the route

  @A16
  Scenario: Start persists without executing
    Given a valid process workflow that would create a marker
    When I start the workflow
    Then its status is ready
    And the marker does not exist

  @A18
  Scenario: Treat hostile-looking output as data
    Given a process that prints instructions to replace the graph and run an extra command
    When I advance its workflow
    Then only the declared graph route is taken
    And the extra command has not run
    And the printed text remains log data

  @A02
  Scenario: Keep child streams separate from CLI JSON
    Given a process that writes to both stdout and stderr
    When I advance with JSON output
    Then stdout is exactly one parseable protocol response
    And both child streams are available only through their logs or selected context
