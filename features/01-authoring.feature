@contract @P04
Feature: Compile explicitly marked Markdown workflows
  The engine validates definitions without executing workflow actions.

  @A01
  Scenario: Select one workflow among prose and ordinary diagrams
    Given a document containing two valid workflow pairs "first" and "second" and an unmarked Mermaid diagram
    When I validate the document selecting "second"
    Then validation succeeds for "second"
    And no child process has been launched

  @A01
  Scenario: Require selection when multiple workflows exist
    Given a document containing two valid workflow pairs
    When I validate without selecting a workflow
    Then validation fails with a source diagnostic
    And no run has been created

  @A05 @A19
  Scenario Outline: Reject invalid metadata before any execution
    Given an otherwise valid workflow with <defect>
    When I start that workflow
    Then the CLI exits with code 3
    And a diagnostic identifies the offending source line and column
    And no run or process side effect has been created
    Examples:
      | defect                              |
      | a duplicate node key                |
      | an unknown node field               |
      | a YAML alias                        |
      | a YAML merge key                    |
      | an explicit YAML tag                |
      | a second YAML document              |
      | a numeric argument without quotes   |
      | an unsupported schema version       |
      | a missing metadata node             |
      | an unmatched workflow marker        |
      | an invalid context selector         |

  @A05
  Scenario Outline: Reject an ambiguous or unsupported graph
    Given an otherwise valid workflow with <defect>
    When I validate the document
    Then validation fails with a source diagnostic
    And no child process has been launched
    Examples:
      | defect                              |
      | an undeclared edge target           |
      | a duplicate node declaration        |
      | duplicate outgoing outcome labels   |
      | mixed labeled and unlabeled edges   |
      | an agent default edge               |
      | an end node with an outgoing edge   |
      | an unreachable node                 |
      | a node with no path to an end        |
      | a subgraph statement                |
      | an implicit node declaration        |
      | a click directive                   |

  @A19
  Scenario: Preserve a multiline YAML prompt
    Given an agent workflow with a literal YAML block prompt and quoted numeric string arguments in a process node
    When I validate and advance the workflow to its agent node
    Then the prompt equals the decoded YAML string including its final newline
    And validation preserves every argument as a string

  @A01
  Scenario: Ignore executable-looking examples inside a longer fence
    Given a document with a valid workflow and a four-backtick fenced example containing three-backtick workflow fences
    When I validate without selecting a workflow
    Then only the outer document's valid workflow is selected

  @A14
  Scenario: Keep the compiled snapshot after source changes
    Given a started workflow whose next process writes "original" to a marker
    When I change its source metadata to write "modified"
    And I advance the existing run
    Then the marker contains "original"

  @A05
  Scenario Outline: Reject an oversized document or graph
    Given a workflow with <size>
    When I validate the document
    Then validation fails before execution
    Examples:
      | size                  |
      | 101 nodes             |
      | 251 edges             |
      | 1048577 UTF-8 bytes   |
