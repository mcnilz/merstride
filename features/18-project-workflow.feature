@P18 @A09
Feature: Simple project workflow selection
  Scenario: Start and inspect the only project run without setup
    Given an isolated project with a workflow
    When I start and inspect it without workspace state or run arguments
    Then the ready run uses the project workspace and state root

  Scenario: Complete a project handoff with stable short selectors
    Given an isolated project with a workflow
    When I perform a looping handoff with short run and request prefixes
    Then retries return the original receipt without completing a newer request

  Scenario: Refuse to choose between multiple runs including completed runs
    Given an isolated project with a workflow
    When I create two runs and attempt an implicit selection
    Then selection fails without changing either run

  Scenario: Refuse ambiguous prefixes and retain exact targeting
    Given an isolated project with a workflow
    When stored runs and requests share short prefixes
    Then ambiguous selections fail and full IDs still select the original resources

  Scenario Outline: Project discovery and explicit compatibility
    Given an isolated project with a workflow
    When I inspect project storage from <location>
    Then storage discovery preserves the selected root workspace and saved IDs
    Examples:
      | location          |
      | subdirectory      |
      | legacy-directory  |
      | outside-project   |
      | nested-git        |
      | explicit-root     |
      | git-file          |

  Scenario Outline: Refuse unsafe or unusable selections
    Given an isolated project with a workflow
    When I select project storage with <defect>
    Then selection reports <code> with exit <exit> without changing stored content
    Examples:
      | defect           | code              | exit |
      | empty-root       | RUN_NOT_FOUND     | 4    |
      | missing-run      | IO_ERROR          | 6    |
      | corrupt-run      | CORRUPT_STATE     | 6    |
      | missing-snapshot | IO_ERROR          | 6    |
      | root-file        | IO_ERROR          | 6    |
      | marker-file      | IO_ERROR          | 6    |
      | run-path         | INVALID_RUN_ID    | 4    |
      | short-run        | INVALID_RUN_ID    | 4    |
      | stale-prefix     | RUN_NOT_FOUND     | 4    |
      | request-path     | INVALID_REQUEST   | 4    |
      | short-request    | INVALID_REQUEST   | 4    |
      | stale-request    | INVALID_REQUEST   | 4    |

  Scenario: Preserve existing ignore and configuration content on first use
    Given an isolated project with a workflow
    When I start with existing ignore and configuration content
    Then only a narrow state ignore rule is appended and no content is replaced

  Scenario: Process execution and logs use the discovered project workspace
    Given an isolated project with a workflow
    When I start from a subdirectory and advance a process using defaults
    Then the process effect and selected logs belong to the project root

  Scenario: Offline discovery ignores conflicting project state
    Given an isolated project with a workflow
    When I read all offline discovery commands with a conflicting state marker
    Then discovery succeeds without modifying the marker

  Scenario: Protect state when existing ignore rules negate an earlier run rule
    Given an isolated project with a workflow
    When I start with an existing negated run ignore rule
    Then existing ignore bytes are preserved and the final rule ignores runs
