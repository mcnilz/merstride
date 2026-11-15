@contract @P05
Feature: Enforce execution and context limits
  @A12
  Scenario: Time out and terminate a process tree
    Given a process node with a one-second timeout and an attached child waiting indefinitely
    And a "timeout" edge to a failure end
    When I advance the workflow
    Then the timeout route is taken within the test watchdog deadline
    And the parent and attached child have terminated

  @A12
  Scenario: Bound combined output without deadlocking
    Given a process concurrently writing more than 10 MiB across stdout and stderr
    And an "output_limit" edge
    When I advance the workflow
    Then the output_limit route is taken within the test watchdog deadline
    And combined retained logs are no larger than 10 MiB

  @A12
  Scenario Outline: Enforce the transition boundary
    Given a workflow with maxTransitions 2 whose cursor reaches <target> on the second transition
    When I advance until the next stopping point
    Then the result is <result>
    Examples:
      | target             | result                  |
      | a success end      | completed               |
      | a process node     | failed TRANSITION_LIMIT |
      | an agent node      | failed TRANSITION_LIMIT |

  @A04 @A12
  Scenario: Return a bounded Unicode log tail
    Given a completed process with more than 4096 bytes of multibyte UTF-8 output
    When an agent requests its stdoutTail
    Then the tail is valid UTF-8 and no larger than 4096 bytes
    And truncation and observed output size are reported

  @A12
  Scenario: Reject excessive selected context
    Given valid individual prompt and data values whose assembled prompt and context exceed 32768 bytes
    When the agent node is activated
    Then the run fails with "CONTEXT_LIMIT"
    And no silently truncated structured context is returned

  @A06
  Scenario Outline: Enforce structured data boundaries
    Given a run waiting for an agent
    When I complete with data having <property>
    Then acceptance is <decision>
    Examples:
      | property                       | decision |
      | exactly 16384 serialized bytes | allowed  |
      | 16385 serialized bytes         | rejected |
      | nesting depth 16               | allowed  |
      | nesting depth 17               | rejected |
      | duplicate object keys          | rejected |
      | integer 9007199254740992        | rejected |

  @A16
  Scenario: Read only the requested activation log
    Given two process activations with different stdout and stderr logs
    When I request 100 tail bytes of the first activation's stderr
    Then only that stream's tail is returned with truncation metadata
    And no child process is launched
