@P19 @A09 @A17
Feature: Workflow context and deliberate run retention
  Scenario: Continue the selected workflow among independent stored runs
    Given a project with two workflow files
    When I start both workflows and start the first again
    Then each start creates a distinct nonexecuting run
    And separate commands continue the first workflow without run IDs

  Scenario: Use canonical short identities without collision reuse
    Given a project with two workflow files
    When I create runs and looping requests with reserved identity collisions
    Then eight-character identities remain distinct and historical retries stay safe

  Scenario: Switch workflow context using paths and deliberate selection
    Given a project with two workflow files
    When I start both workflows and start the first again
    And I select workflow paths from a project subdirectory
    Then frozen runs remain associated with their original file and selected workflow

  Scenario: A delayed completion finds its original request after context changes
    Given a project with two workflow files
    When I complete and retry earlier requests while another run is current
    Then newer requests and unrelated runs remain unchanged

  Scenario: Exact short identities win over longer matching identities
    Given a project with two workflow files
    When exact short and longer run and request identities share their prefix
    Then every exact identity targets its own resource

  Scenario: Discard one obsolete run without changing another workflow
    Given a project with two workflow files
    When I discard a selected completed run with request history
    Then only its state logs and receipts are removed and identities are not reused

  Scenario Outline: Context boundaries and recovery remain explicit
    Given a project with two workflow files
    When I exercise context <case>
    Then context selection fails closed or selects the intended frozen run
    Examples:
      | case                 |
      | multiple-pairs       |
      | renamed-source       |
      | stale-current        |
      | corrupt-context      |
      | null-context-source  |
      | duplicate-context    |
      | missing-selected-snapshot |
      | context-path-conflict |
      | missing-context      |
      | terminal-current     |
      | explicit-roots       |
      | context-lock         |
      | identity-lock        |
      | legacy-state         |
      | extended-identities  |
      | unreadable-request-search |
      | simultaneous-writer |
      | positional-workflow |
      | positional-multiple-pairs |
      | positional-other-commands |
      | id-shaped-source |
      | literal-delimiter |
      | selector-conflicts |

  Scenario: Read a complete multi-workflow handoff in ordinary text
    Given a project with two workflow files
    When I follow the readable contextual handoff
    Then the readable path completes without run IDs or JSON tools

  Scenario Outline: Discard protects locked unreadable and unowned data
    Given a project with two workflow files
    When I exercise discard <case>
    Then discard preserves unrelated data and reports its actual result
    Examples:
      | case              |
      | writer-lock       |
      | corrupt-state     |
      | unknown-file      |
      | unknown-directory |
      | linked-logs       |
      | read-only-state   |
      | staged-failure    |
      | staged-retry      |
      | empty-staged-retry |
      | receipt-only-retry |
      | conflicting-staged-file |
      | unowned-empty-stage |
      | process-logs      |
