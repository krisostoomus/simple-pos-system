Feature: Purchase flow
  As a seller I can add items and check out, receiving correct change.

  Scenario: Buy two brownies and pay with a five euro note
    Given the POS app is open
    When I click the "Brownie" product 2 times
    Then the running total shows "1.30"
    When I checkout with cash "5.00"
    Then the change shown is "3.70"
