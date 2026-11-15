@contract @P07
Feature: Deliver a usable native agent interface
  @A20 @P01
  Scenario: Discover the agent protocol offline
    Given an installed native binary with no workflow or run
    When I invoke guide with JSON output
    Then the CLI exits with code 0 and returns a guide string
    And the guide explains start, advance, status, complete, allowed outcomes, and interruption recovery
    And no run, child process, or network request is created

  @A15
  Scenario Outline: Run without an installed language runtime
    Given the published native binary on a clean <platform> with no .NET SDK or runtime
    And a bundled native acceptance fixture process
    When I execute the native acceptance suite
    Then all release-applicable scenarios pass
    Examples:
      | platform       |
      | Windows 11 x64 |
      | Linux x64      |

  @A16
  Scenario Outline: Keep inspection commands read-only
    Given a valid workflow and an existing run with logs
    When I invoke <command>
    Then no workflow process starts and no run state is modified
    Examples:
      | command  |
      | validate |
      | status   |
      | logs     |
      | guide    |
      | version  |

  @A16
  Scenario: Inspect failure without returning an execution exit code
    Given a failed run
    When I query status with JSON output
    Then the CLI exits with code 0
    And the response status is failed

  @A09
  Scenario: Resume using an explicit state directory from another working directory
    Given a started run and its returned absolute state root
    When a fresh CLI process in another directory queries the run using that state root
    Then the correct persisted run is returned

  @A15
  Scenario: Meet native distribution budgets
    Given a release candidate and a documented reference machine
    When I measure its uncompressed executable and 30 warm status process starts
    Then executable size excluding separate symbols is at most 25 MiB
    And warm status latency P95 is below 150 milliseconds
    And first-start latency and the 15 MiB size target result are reported separately
