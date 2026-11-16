@P11 @A01 @A05 @A14 @A19 @A20 @A16
Feature: Author workflows using the installed offline creation guide

  Scenario Outline: Retrieve the creation guide from a standalone native binary
    Given only the published native executable in an isolated directory
    When I invoke the creation guide with arguments <arguments>
    Then one successful protocol response contains self-contained authoring instructions
    And the isolated directory contains only the unchanged executable

    Examples:
      | arguments |
      | creation-guide |
      | creation-guide;--json;--state-dir;not-absolute |
      | --json;--state-dir;not-absolute;creation-guide |
      | --state-dir;not-absolute;creation-guide;--json |
      | creation-guide;--state-dir;not-absolute;-- |
      | creation-guide;--state-dir;{binary};--json |
      | --state-dir;{binary}/unusable;creation-guide |

  Scenario: Validate the complete example extracted from the actual embedded guide
    Given only the published native executable in an isolated directory
    When I invoke the creation guide with arguments creation-guide;--json
    Then one successful protocol response contains self-contained authoring instructions
    When I extract and validate the complete example from that response
    Then the example validates without running its process or creating state

  Scenario Outline: Discover the creation command through English help
    Given only the published native executable in an isolated directory
    When I invoke the creation guide with arguments <arguments>
    Then English help describes the workflow authoring command
    And the isolated directory contains only the unchanged executable

    Examples:
      | arguments |
      | --help |
      | -h;--json;--state-dir;not-absolute |
      | --help;--json |
      | -h |
      | creation-guide;--help;--state-dir;not-absolute |
      | creation-guide;-h |
      | creation-guide;--help;--json |
      | --json;creation-guide;-h;--state-dir;not-absolute |

  Scenario Outline: Reject invalid authoring-guide arguments without effects
    Given only the published native executable in an isolated directory
    When I invoke the creation guide with arguments <arguments>
    Then a protocol usage error includes a help hint
    And the isolated directory contains only the unchanged executable

    Examples:
      | arguments |
      | creation-guide;extra;--json |
      | creation-guide;--workflow;review;--json |
      | creation-guide;--unknown;--json |
      | creation-guide;--json;--json |
      | creation-guide;--state-dir |
      | creation-guide;--state-dir;--json |
      | creation-guide;--state-dir;x;--state-dir;y |
      | creation-guide;--help;-h |
      | creation-guide;extra;--help;--json |
      | creation-guide;--unknown;--help;--json |
      | creation-guide;--version |
      | creation-guide;--json=false |
