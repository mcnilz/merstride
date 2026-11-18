@P24 @A02 @A07
Feature: Process output reaches the caller in the execution command

  Scenario: Receive both streams while the process is still running
    Given a controlled process that exits with 0
    When I start and advance with default process output
    Then both streams are available in this invocation without log files

  Scenario: Keep both streams when the process fails
    Given a controlled process that exits with 7
    When I start and advance with default process output
    Then both streams are available in this invocation without log files

  Scenario Outline: Retain only the requested streams
    Given a controlled process that exits with 0
    When I advance saving <streams>
    Then only <streams> can be read with logs

    Examples:
      | streams          |
      | stdout           |
      | stderr           |
      | stdout and stderr |

  Scenario: Route a timeout when the caller stops reading stdout
    When I execute a process that fills the redirected stdout pipe
    Then blocked delivery does not prevent the saved timeout route
