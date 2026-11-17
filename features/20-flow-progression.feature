@P20 @A02 @A07
Feature: Follow the flow and understand its result

  Scenario: Follow exact and default branches to a successful end
    Given a progression workflow with two process branches
    When I advance the progression as text
    Then process results and numbered flow movements explain both branch choices in order
    And the final summary distinguishes the last process outcome from overall success

  Scenario: Follow an agent loop and retry the original receipt
    Given a progression workflow with a looping agent
    When I advance the looping agent as text
    Then waiting explains that the flow stays at review and requires completion
    When I complete the loop with outcome revise
    Then the accepted completion explains review to work without executing work
    When I advance through the repeated process visit
    Then the repeated visits are identified before the new request
    And retrying the original completion returns its receipt without progression
    When I complete the loop with outcome approved
    And I advance the finished loop
    Then terminal inspection and repeated advance have no new activity

  Scenario Outline: Explain a process outcome separately from a failed run
    Given a progression process with <kind>
    When I advance the failing progression
    Then the failure explains <cause> and no route or automatic recovery
    And read-only failed status succeeds with the same result and next action

    Examples:
      | kind         | cause                                        |
      | no-match     | No route matches exit_7 at work               |
      | failure-end  | Workflow reached a failure end               |
      | loop-limit   | Transition budget exhausted                  |

  Scenario Outline: Explain a non-exit process outcome in plain English
    Given a progression process with <kind>
    When I advance the failing progression
    Then the saved process explanation is <explanation> and overall success is separate

    Examples:
      | kind         | explanation                                      |
      | spawn_error  | process could not be launched (spawn_error)       |
      | timeout      | process time limit reached (timeout)              |
      | output_limit | process output limit reached (output_limit)       |

  Scenario: Explain an unlabeled edge without treating an exit code as success
    Given a progression process with unlabeled
    When I advance the failing progression
    Then the unlabeled flow explains that it accepts failures too

  Scenario: Reject completions and failed saves without claiming movement
    Given a progression workflow with a looping agent
    When I advance the looping agent as text
    Then rejected outcomes and rejected completion saves explain inspection without acceptance

  Scenario: Keep progress separate from escaped task data under redirection
    Given a progression workflow with a looping agent
    When I advance the looping agent as text
    Then redirected streams retain escaped prompt data and compact progress

  Scenario: Preserve structured integration across a loop and receipt retry
    Given a progression workflow with a looping agent
    When I execute the progression loop with explicit JSON
    Then all responses are single protocol-1 objects without human activity

  Scenario: Retain live visibility and truthful failed result saves
    Given a progression process held at a native barrier
    When I observe its live activation before release
    Then a rejected result save claims no result route or terminal success

  Scenario: Explain real interruption with no invented movement
    When I interrupt progression through the native signal fixture
    Then interruption describes uncertain effects and explicit resolution without replay

  Scenario Outline: Explain an explicit interrupted-run decision
    When I recover interrupted progression with <decision>
    Then the decision describes an unchanged cursor and the next explicit action

    Examples:
      | decision |
      | retry    |
      | fail     |

  Scenario Outline: Preserve process-like names as agent choices
    Given a progression agent allowing <outcome>
    When I accept its process-like outcome as text
    Then the summary describes an agent choice and JSON retains the literal outcome

    Examples:
      | outcome      |
      | exit_0       |
      | timeout      |
      | spawn_error  |
      | signal       |
      | output_limit |
