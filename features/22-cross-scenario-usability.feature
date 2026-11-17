@P22 @A20 @A16
Feature: Discover retained-run management without operational setup
  Scenario Outline: Read offline management help without selecting or changing a run
    Given an isolated CLI discovery directory
    When I invoke management help using <arguments>
    Then offline management help succeeds in <format> without effects
    Examples:
      | arguments | format |
      | runs;--help;--state-dir;not-absolute | text |
      | --json;runs;-h;--state-dir;not-absolute | json |
      | runs;workflow.md;--help;--state-dir;not-absolute | text |
      | --json;runs;workflow.md;-h;--state-dir;not-absolute | json |
      | use;--help;--state-dir;not-absolute | text |
      | --json;use;-h;--state-dir;not-absolute | json |
      | use;r_000001;--help;--state-dir;not-absolute | text |
      | --json;use;r_000001;-h;--state-dir;not-absolute | json |
      | discard;--help;--state-dir;not-absolute | text |
      | --json;discard;-h;--state-dir;not-absolute | json |
      | discard;r_000001;--help;--state-dir;not-absolute | text |
      | --json;discard;r_000001;-h;--state-dir;not-absolute | json |
