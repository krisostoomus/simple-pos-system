Feature: Localization
  The UI and product names can switch language.

  Scenario: Switch to Estonian
    Given the POS app is open
    When I switch the language to Estonian
    Then the checkout button reads "Maksma"
