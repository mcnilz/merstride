@P17 @A02 @A07
Feature: Live console activity without changing execution

  Scenario: Observe activation before a controlled process finishes
    Given a console activity workflow with a controlled process
    When I advance it with redirected text output
    Then its activation is visible before the process is released
    And its saved outcome and terminal transition appear in order

  Scenario: Show every process transition before an agent handoff
    Given a console activity chain ending at an agent
    When I advance the chain as text
    Then both outcomes and routes precede the agent waiting message
    And inspection and repeated waiting advance announce no new work
    And rejected completion announces no acceptance
    And accepted completion is saved once and executes nothing
    And final advance reports completion only once

  Scenario Outline: Explain process outcomes and routing failures
    Given a console activity process producing <outcome>
    When I advance the process as text
    Then activity reports <outcome> and <result>

    Examples:
      | outcome      | result           |
      | spawn_error  | routed           |
      | timeout      | routed           |
      | exit_7       | NO_MATCHING_EDGE |
      | output_limit | routed           |
      | exit_0       | WORKFLOW_FAILURE |

  Scenario: Preserve one JSON response without progress
    Given a console activity chain ending at an agent
    When I execute the handoff and completion with explicit JSON
    Then every JSON call has one protocol response and no activity

  Scenario: Keep terminal controls out of feedback and preserve raw logs
    Given a console activity workflow containing terminal controls
    When I advance the process as text
    Then text feedback is escaped and bounded while raw logs remain unchanged

  Scenario: Do not claim a process result was saved when persistence fails
    Given a console activity workflow with a controlled process
    When I advance it with redirected text output
    Then a failed result save announces neither a saved outcome nor completion

  Scenario: Explain a real user interruption without replay
    When I interrupt a text advance through the native signal fixture
    Then interruption feedback requires inspection and explicit resolution

  Scenario: Explain transition exhaustion without activating more work
    Given a console activity loop with one allowed transition
    When I advance the process as text
    Then the budget error follows one saved outcome without another activation

  Scenario: Keep execution intact when the activity consumer closes its pipe
    Given a console activity workflow with a controlled process
    When the activity consumer closes its pipe before process completion
    Then the saved run still completes successfully
