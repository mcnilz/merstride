@P08 @A20
Feature: Discover and invoke the CLI predictably

  Scenario Outline: Read English help without workflow or state access
    Given an isolated CLI discovery directory
    When I request <command> help using <flag> as <format>
    Then useful English help succeeds without side effects

    Examples:
      | command  | flag   | format |
      | root     | --help | text   |
      | root     | -h     | json   |
      | validate | --help | text   |
      | validate | -h     | json   |
      | start    | --help | text   |
      | start    | -h     | json   |
      | advance  | --help | text   |
      | advance  | -h     | json   |
      | status   | --help | text   |
      | status   | -h     | json   |
      | complete | --help | text   |
      | complete | -h     | json   |
      | resolve  | --help | text   |
      | resolve  | -h     | json   |
      | logs     | --help | text   |
      | logs     | -h     | json   |
      | guide    | --help | text   |
      | guide    | -h     | json   |
      | version  | --help | text   |
      | version  | -h     | json   |

  Scenario Outline: Reject invalid arguments before accessing files
    Given an isolated CLI discovery directory
    When I invoke the literal CLI arguments <arguments>
    Then one JSON usage error includes a help hint without side effects

    Examples:
      | arguments |
      | --json |
      | unknown;--json |
      | version;--unknown;--json |
      | version;extra;--json |
      | status;--json;--json |
      | status;r_missing;extra;--json |
      | start;--json |
      | start;missing.md;--workspace;--json |
      | version;--state-dir;--json |
      | version;--state-dir;--unknown;--json |
      | version;--json;--json |
      | --json;version;--json |
      | version;--state-dir;{state};--state-dir;{state};--json |
      | validate;missing.md;--workflow;x;--workflow;y;--json |
      | resolve;r_missing;--retry;--retry;--json |
      | resolve;r_missing;--fail;--fail;--json |
      | resolve;r_missing;--retry;--fail;--json |
      | resolve;r_missing;--json |
      | status;r_missing;--retry;--json |
      | complete;r_missing;--request;q_missing;--json |
      | logs;r_missing;--activation;a_missing;--stream;other;--json |
      | logs;r_missing;--activation;a_missing;--stream;stdout;--tail-bytes;0;--json |
      | version;--json=false |
      | version;--version;--json |
      | version;--help;-h;--json |
      | version;--unknown;--help;--json |
      | version;extra;--help;--json |
      | resolve;r_missing;--retry;--fail;--help;--json |
      | --unknown;--help;--json |

  Scenario Outline: Discover version without reading storage
    Given an isolated CLI discovery directory
    When I invoke the literal CLI arguments <arguments>
    Then one JSON version response succeeds without side effects

    Examples:
      | arguments |
      | --version;--json |
      | --json;--state-dir;not-absolute;--version |
      | version;--state-dir;not-absolute;--json |

  Scenario Outline: Place globals around the command and delimit literal paths
    Given a valid workflow whose filename is <filename>
    When I validate with globals <placement> the command and a literal delimiter
    Then validation succeeds without creating state

    Examples:
      | filename              | placement |
      | workflow with spaces.md | before  |
      | --option-like file.md | after     |
      | --help                | before    |
      | @response.md          | after     |

  Scenario: Read an at-sign path without response-file expansion
    Given a valid workflow whose filename is @response.md
    When I validate the at-sign path without a delimiter
    Then validation succeeds without creating state

  Scenario: Start with option-like input filename and a delimited workflow path
    Given a valid workflow whose filename is --workflow with spaces.md
    When I start with an equals-form input path and options before the delimiter
    Then a ready run is returned without workflow execution
