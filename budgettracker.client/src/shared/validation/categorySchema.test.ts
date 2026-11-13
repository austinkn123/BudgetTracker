import { describe, expect, it } from "vitest";

import { categorySchema } from "./categorySchema";

describe("categorySchema", () => {
  // The Select empty option ("No automatic mapping") writes null to the form.
  it("accepts a null plaidCategoryPrimary", () => {
    const result = categorySchema.safeParse({
      name: "Groceries",
      categoryType: "Expense",
      plaidCategoryPrimary: null,
    });

    expect(result.success).toBe(true);
  });

  it("still accepts a string or a missing plaidCategoryPrimary", () => {
    expect(
      categorySchema.safeParse({
        name: "A",
        categoryType: "Expense",
        plaidCategoryPrimary: "FOOD_AND_DRINK",
      }).success,
    ).toBe(true);
    expect(
      categorySchema.safeParse({ name: "A", categoryType: "Expense" }).success,
    ).toBe(true);
  });
});
