@contract @P01 @A09 @A16
Feature: Start with a minimal native workflow
  Scenario: Persist and finish a single terminal node
    Given an isolated workspace and state directory
    And a marked YAML and Mermaid workflow containing only a success end node
    When I start the workflow with the published native binary
    Then the run is ready and no child process is launched
    When a fresh CLI process queries the returned run ID and state root
    Then the same ready run is returned
    When I advance that run
    Then the run is completed with CLI exit code 0
