Feature: Out of stock
  Second-hand items start at zero stock and are grayed out.

  Scenario: A zero-stock item is disabled
    Given the POS app is open
    Then the "Jacket" product is grayed out
