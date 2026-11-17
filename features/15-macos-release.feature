@contract @P15 @A16
Feature: Observe native process launches on every release platform
  Scenario: Retain short-lived descendant launches through external observation
    Given the published native process fixture and external platform observer
    When the fixture launches 32 short-lived native descendants
    Then all 33 native launches are observed after they have exited
