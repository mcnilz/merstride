@P21
Feature: Explicitly start and first advance in one native invocation
  Ordinary start remains nonexecuting; combined execution targets its new run.

  Scenario Outline: Match separate start and advance effects and stopping conditions
    Given a combined-start workflow ending at <stop>
    When I compare separate start and advance with start --advance as <format>
    Then both paths have equivalent progress and effects at <stop>
    Examples:
      | stop  | format |
      | chain | json   |
      | chain | text   |
      | handoff | json |
      | handoff | text |
      | agent | json |
      | agent | text |
      | success | json |
      | success | text |
      | failure | json |
      | failure | text |
      | no-route | json |
      | no-route | text |
      | transition-limit | json |
      | spawn | json |
      | timeout | json |
      | output-limit | json |

  Scenario Outline: Reject creation failures without executing an existing run
    When combined start fails before creation because of <defect>
    Then no node executes and the existing ready run is unchanged
    Examples:
      | defect |
      | malformed workflow |
      | invalid input |
      | missing workspace |
      | preparation conflict |
      | initial persistence conflict |
      | context lock |
      | corrupt context |
      | duplicate flag |
      | false flag |

  Scenario Outline: Keep identity and durable progress after first-advance errors
    When a combined first advance encounters <failure> in <format>
    Then the created run is recoverable without rollback or automatic replay
    Examples:
      | failure | format |
      | request lock | json |
      | result save | json |
      | result save | text |

  Scenario: Preserve exact targeting while other callers change selection
    When combined start runs behind a barrier while same and different workflows start
    Then only the created run advances using its frozen input and workflow

  Scenario Outline: Interrupt combined execution and recover explicitly
    When I interrupt a combined start in <format> and choose <decision>
    Then cancellation retains identity and requires explicit recovery without replay
    Examples:
      | format | decision |
      | json | retry |
      | text | fail |
