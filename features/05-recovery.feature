@contract @P06
Feature: Recover without silently repeating side effects
  @A10
  Scenario: Exclude a second writer on the same run
    Given advance is holding a run lock while a test process waits on a barrier
    When a second CLI process attempts to advance the same run
    Then it exits with code 4 and error "RUN_LOCKED"
    And the child process has started only once
    And another run can advance independently

  @A11
  Scenario: Detect a crash after an external effect
    Given a process has written an external marker and is waiting on a barrier before exiting
    When the engine process is forcibly terminated
    And a fresh CLI process reads status
    Then the run is reported interrupted without modifying persisted state
    When I advance that run
    Then no additional process is started
    And the CLI exits with code 5

  @A11
  Scenario: Explicitly retry an uncertain activation
    Given an interrupted process activation
    When I resolve with retry
    Then the run is ready and no process has started
    When I advance
    Then a new activation ID is used
    And the previous activation remains recorded as interrupted

  @A11
  Scenario: Fail an interrupted run without further effects
    Given an interrupted process activation
    When I resolve with fail
    Then the run is failed and the CLI exits with code 5
    And no child process is started

  @A11
  Scenario: Handle a user interrupt
    Given an executing process with an attached child
    When I send the platform's supported user-interrupt signal
    Then the CLI exits with code 130
    And the process tree is terminated on the supported test platform
    And a fresh status reports interrupted

  @A11
  Scenario: Refuse corrupt state
    Given a persisted run whose authoritative state file has been corrupted
    When I read its status
    Then the CLI exits with code 6
    And the corrupt state is not replaced or reset

  @A10
  Scenario: Readers never observe partially written JSON
    Given a writer repeatedly commits valid transitions while holding its run lock
    When independent readers repeatedly query status
    Then every successful response is one complete protocol JSON object
    And every reported cursor and status is a committed combination
