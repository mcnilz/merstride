@build
Feature: Enforce architecture without shipping the analyzer
  @A21 @P01
  Scenario: Reject a forbidden namespace dependency during compilation
    Given a build fixture with the production NsDepCop configuration
    And its Domain code references an Infrastructure type
    When I build the fixture
    Then the build fails with diagnostic NSDEPCOP01 as an error

  @A21 @P01
  Scenario: Accept an intended dependency
    Given a build fixture with the production NsDepCop configuration
    And its Application code references a Domain type without any forbidden dependency
    When I build the fixture
    Then the build succeeds with architecture analysis enabled

  @A21 @P01
  Scenario Outline: Reject ineffective architecture enforcement
    Given a production project with <defect>
    When I execute the architecture build gate
    Then the gate fails and identifies the ineffective enforcement
    Examples:
      | defect                                  |
      | a missing NsDepCop configuration        |
      | architecture analysis disabled          |
      | illegal dependencies suppressed globally |

  @A22 @P07
  Scenario: Keep the GPL analyzer outside MIT product distribution
    Given a published Native AOT release candidate
    When I inspect resolved assets, compiler references, native publish inputs, and release contents
    Then no NsDepCop implementation or analyzer-only dependency is included as product code or runtime assets
    And no analyzer source or binary is bundled in the release
    And project-owned source has an MIT license
    And shipped dependencies have compatible permissive licenses and required notices
    And NsDepCop appears only in the separate build-tool inventory
