@build
Feature: Deliver an established and maintainable GitHub project
  @A23 @P01
  Scenario: Reject unapproved actions and unresolved action pins
    Given a workflow using an action outside the official allowlist or without a verified full commit SHA
    When I run the workflow policy checks
    Then the build gate fails and identifies the action reference

  @A23 @P01
  Scenario: Accept a contribution without publication privileges
    Given a pull request from a fork
    When the build workflow runs
    Then the build and implemented acceptance checks run with read-only repository permissions
    And no release is published and no repository secret is exposed to the contribution

  @A24 @P01
  Scenario: Reject an undocumented dependency
    Given a proposed direct or transitive dependency missing its required provenance or license review
    When I run the dependency review gate
    Then adoption is blocked and the missing evidence is reported

  @A25 @P07
  Scenario: Publish only complete artifacts from the tested tag
    Given an authorized valid release tag on main
    And every required build, platform, architecture, acceptance, and licensing gate has passed for its commit
    When the release workflow publishes
    Then the release contains exactly the verified platform archives and supporting files from that run
    And their versions, commit identity, and checksums match

  @A25 @P07
  Scenario Outline: Block unsafe or incomplete publication
    Given a release attempt with <defect>
    When the release publication gate runs
    Then no new public release or overwritten published asset is produced
    Examples:
      | defect                                |
      | a failed required test                |
      | a missing advertised platform archive |
      | an artifact from another commit       |
      | a tag outside main                    |
      | an already published version          |
