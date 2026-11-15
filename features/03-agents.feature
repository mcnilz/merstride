@contract @P03
Feature: Hand work to the existing agent
  Background:
    Given an isolated workspace and state directory

  @A03
  Scenario: Repair and recheck
    Given a check that initially fails and routes to an agent with outcomes "recheck" and "blocked"
    When I advance the workflow
    Then it waits for the agent
    When the agent repairs the workspace and completes with "recheck"
    Then the run is ready and the check has not run again
    When I advance the run
    Then the check succeeds and the workflow completes

  @A04
  Scenario: Expose only requested context
    Given an agent node selecting only "nodes.check.exitCode"
    And other nodes have completed with prompts, data, and large logs
    When the run reaches the agent node
    Then its context contains only "nodes.check.exitCode"
    And the response omits other prompts, full logs, and completion history

  @A09
  Scenario: Resume a waiting request from a new CLI process
    Given a run waiting for an agent
    When a fresh CLI process reads status and calls advance
    Then both responses contain the original request ID and task
    And the transition count is unchanged

  @A06
  Scenario Outline: Reject invalid completion without mutation
    Given a run waiting for an agent
    When I submit <completion>
    Then the command rejects it with <error>
    And persisted run state is unchanged
    Examples:
      | completion                   | error           |
      | an unknown request ID        | INVALID_REQUEST |
      | an undeclared outcome        | INVALID_OUTCOME |
      | data larger than 16384 bytes | DATA_LIMIT      |

  @A07
  Scenario: Safely repeat an accepted completion
    Given an accepted completion with data containing two object keys
    When I submit the same request and values with reversed object key order
    Then the original receipt and current status are returned
    And no additional transition or process execution occurs

  @A08
  Scenario: Reject a conflicting historical completion
    Given an accepted completion
    When I resubmit its request ID with different data
    Then the command fails with "COMPLETION_CONFLICT"
    And persisted run state is unchanged

  @A17
  Scenario: Distinguish requests across loop iterations
    Given a completed agent request followed by a loop back to the same agent node
    When the node is activated again
    Then its request ID differs from the previous request ID
    When I resend the previously accepted completion
    Then its historical receipt is returned
    And the new request remains waiting

  @A04
  Scenario: Report unavailable and previous-iteration context explicitly
    Given a loop whose agent selects data from a node not yet completed on its first visit
    When I inspect the first request
    Then that selector has available false
    When the selected node completes and the agent is visited again
    Then the selector exposes the latest completed value and its activation ID
