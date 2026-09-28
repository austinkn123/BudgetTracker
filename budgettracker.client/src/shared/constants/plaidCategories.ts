/**
 * Plaid's personal_finance_category.primary taxonomy. A BudgetTracker category can claim one of
 * these, and imported transactions carrying that value then resolve to it automatically (BUD-9).
 */
export const PLAID_PRIMARY_CATEGORIES = [
  'INCOME',
  'TRANSFER_IN',
  'TRANSFER_OUT',
  'LOAN_PAYMENTS',
  'BANK_FEES',
  'ENTERTAINMENT',
  'FOOD_AND_DRINK',
  'GENERAL_MERCHANDISE',
  'HOME_IMPROVEMENT',
  'MEDICAL',
  'PERSONAL_CARE',
  'GENERAL_SERVICES',
  'GOVERNMENT_AND_NON_PROFIT',
  'TRANSPORTATION',
  'TRAVEL',
  'RENT_AND_UTILITIES',
] as const;

export type PlaidPrimaryCategory = (typeof PLAID_PRIMARY_CATEGORIES)[number];

/** FOOD_AND_DRINK -> "Food and drink" — the taxonomy is SCREAMING_SNAKE_CASE. */
export const formatPlaidCategory = (value: string) => {
  const words = value.toLowerCase().split('_').join(' ');
  return words.charAt(0).toUpperCase() + words.slice(1);
};

export const PLAID_CATEGORY_OPTIONS = PLAID_PRIMARY_CATEGORIES.map((value) => ({
  value,
  label: formatPlaidCategory(value),
}));
