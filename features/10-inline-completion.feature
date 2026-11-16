@P09
Feature: Return agent results directly through CLI parameters

  @A03 @A16
  Scenario: Complete without creating a result file
    Given a waiting inline-completion request
    When I complete it using only an outcome parameter
    Then the run is ready with empty receipt data and no result file
    When I explicitly advance the inline-completion run
    Then the inline-completion run is completed

  @A04 @A06
  Scenario: Return nested Unicode data as one literal argument
    Given a waiting inline-completion request
    When I complete it with nested Unicode inline data
    Then the receipt contains the literal inline data
    When I explicitly advance the inline-completion run
    Then the next request exposes the same literal data

  @A06
  Scenario Outline: Reject invalid input modes without changing state
    Given a waiting inline-completion request
    When I submit completion options <options>
    Then inline completion fails with USAGE and exit 2 without mutation
    Examples:
      | options |
      | missing |
      | --data;{} |
      | --outcome |
      | --outcome;finish;--data |
      | --outcome;finish;--outcome;finish |
      | --outcome;finish;--data;{};--data;{} |
      | --outcome;finish;--result-file;absent.json |
      | --result-file;absent.json;--data;{} |
      | --outcome;finish;--result-file;absent.json;--data;{} |
      | --outcome;finish;extra |
      | --outcome;finish;--input-file;absent.json |
      | --outcome;finish;--help;--result-file;absent.json |
      | --data;{};--help |

  @A06
  Scenario Outline: Apply the existing data limits to inline input
    Given a waiting inline-completion request
    When I complete it with inline data <data>
    Then inline completion has result <result>
    Examples:
      | data                         | result          |
      | {                            | INVALID_PAYLOAD |
      | {} trailing                  | INVALID_PAYLOAD |
      | []                           | INVALID_PAYLOAD |
      | null                         | INVALID_PAYLOAD |
      | "text"                       | INVALID_PAYLOAD |
      | {"x":1,"x":2}                | INVALID_PAYLOAD |
      | {"n":9007199254740991}       | accepted        |
      | {"n":9007199254740992}       | INVALID_PAYLOAD |
      | {"n":1e400}                  | INVALID_PAYLOAD |
      | {"n":NaN}                    | INVALID_PAYLOAD |
      | 16384 compact UTF-8 bytes     | accepted        |
      | 16385 compact UTF-8 bytes     | DATA_LIMIT      |
      | 16384 Unicode UTF-8 bytes     | accepted        |
      | nesting depth 16             | accepted        |
      | nesting depth 17             | INVALID_PAYLOAD |
      | nesting depth 65             | INVALID_PAYLOAD |

  @A06
  Scenario Outline: Reject invalid request or outcome parameters
    Given a waiting inline-completion request
    When I complete it with <invalid>
    Then inline completion fails with <error> and exit 4 without mutation
    Examples:
      | invalid               | error           |
      | an unknown request ID | INVALID_REQUEST |
      | an undeclared outcome | INVALID_OUTCOME |

  @A07 @A08 @A17
  Scenario Outline: Preserve receipts across input modes and later requests
    Given a waiting inline-completion request
    When I first complete using <mode>
    And I explicitly advance the inline-completion run
    Then a new request is waiting
    When I repeat the old completion using <other>
    Then its original receipt returns and the new request stays unchanged
    When I conflict with the old completion using <other>
    Then inline completion fails with COMPLETION_CONFLICT and exit 4 without mutation
    Examples:
      | mode   | other  |
      | inline | file   |
      | file   | inline |

  @A20
  Scenario: Prefer inline completion in offline discovery
    When I inspect inline completion help and the agent guide
    Then both explain outcome parameters and optional JSON data before file compatibility

  @A13 @A06
  Scenario: Pass inline JSON through PowerShell without a result file
    Given a waiting inline-completion request
    When I complete it with nested Unicode inline data through PowerShell
    Then the receipt contains the literal inline data
