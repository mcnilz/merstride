@P25 @A02 @A04 @A07
Feature: Explain workflow execution in plain English

  Scenario: Name the workflow and its steps and summarize success once
    Given a named workflow with a successful process chain
    When I start and advance the named workflow
    Then the workflow and both steps are clear and success is summarized once

  Scenario: Explain why a command stopped the workflow
    Given a named workflow with an unhandled process failure
    When I start and advance the named workflow
    Then its failed step, cause and next action are clear

  Scenario: Make the paused task easy to find and complete
    Given a named workflow with a review handoff
    When I start and advance the named workflow
    Then its paused step, request, prompt, outcomes and next action are clear

  Scenario: Explain a handled failure and repeated step
    Given a named workflow that uses a fallback to retry a step
    When I start and advance the named workflow
    Then its fallback, repeated step and final success are clear
