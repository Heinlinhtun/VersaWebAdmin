# Bundle production verification

Run calculation checks with `dotnet run --project tests/BundleProduction.Tests` and UI checks with `npm test` from `Versa.UI`.

Production uses the current branch, a selected finished-product unit, and a positive whole-number production quantity. Component quantities can be fractional, and selected units are converted to base units. Repeated components share the same stock limit. Services have no stock and do not constrain availability; at least one physical product is required. Finished-product cost is the sum of consumed component costs per finished unit; the bundle price becomes its selling price.

The production endpoint writes Products, ProductStocks, StockAdjustments and ProductionRecipes in a single transaction. ProductionRecipes is created before the transaction begins, avoiding MySQL's implicit DDL commit. It creates no sellable Bundle record. An unchecked checkbox continues to use the standard bundle path.

To produce another batch, choose **Produce** on the production item in **Bundles**. This loads the stored recipe (including services) and opens the focused production form. Use the Bundles edit action to review or change components. Component changes are saved with a successful production. The same product ID, SKU and selling price are retained; stock increases and cost is recalculated using the existing stock and new batch costs. Production is branch-specific.

Production products also appear in the Bundles management list, marked Production. Their Produce action opens only the stock warning, maximum availability, production quantity and Produce controls. It loads the recipe without saving product edits and remains on Bundles after success. The edit action opens the full recipe form. The management list requests `GET /api/bundles?includeProduction=true`; the default endpoint used by POS continues returning only ordinary bundles, preventing duplicate sale-time component deductions.

For products made with the earlier version, an unambiguous single production batch can recover the raw-material recipe from StockAdjustments. Services were not recorded there, so the form asks the user to review the recovered materials and add services once. Where production history cannot establish a recipe, the user must configure it once. The recipe is retained after the next successful production.

## Database and POS acceptance checks

Run these against a disposable test tenant, not production stock:

1. Give Item A 200 base units in branch A and a different quantity in branch B. Add a bundle consuming 10 units of A. Enable production and verify branch A's maximum is 20.
2. Produce 4 units. Verify a standard product with the bundle name and price has stock 4 in branch A, A has stock 160, and branch B is unchanged. Verify one addition and one subtraction in adjustment history.
3. Sell one finished unit through POS. Verify finished stock becomes 3 and component stock stays 160. Return that sale and verify only finished stock is restored.
4. Add A twice, consuming 10 and 15 units per finished product. At stock 200 the maximum must be 8. Repeat with a converted unit (for example a box of 10) and with a second limiting component.
5. Reduce component stock after loading availability. Saving an excessive quantity must fail, refresh availability, and leave no product, stock, or adjustment records from the failed request.
6. Submit two concurrent productions whose combined consumption exceeds available stock. At most one may succeed; verify no partial deductions or orphan products.
7. Force a database failure on adjustment insertion in the test tenant. Verify the product insertion and all stock changes roll back.
8. Verify zero, negative, fractional and excessive production quantities fail; invalid units, inactive products, service-only recipes and missing stock fail appropriately. A non-admin request specifying another branch must still use its authenticated branch.
9. Create and sell an ordinary unchecked bundle. Verify its components are still deducted at sale time.
10. Replenish raw materials, open Bundles, and select Produce on the production item. Verify components and units load automatically, produce another batch, and confirm the product count/ID/SKU are unchanged while stock increases. Reopen and verify the recipe remains available.
11. Change component quantities before another batch; confirm they persist only after successful production. Failed production must leave the previous recipe, cost and stocks unchanged.
12. Test an older product with one production addition and its component deductions, including fractional amounts. Verify recovered quantities per unit and the original finished unit. Missing or ambiguous history must show the one-time setup message. A changed finished unit and a recipe consuming itself must be rejected.
13. Verify weighted average cost (10 existing units costing 10 plus 10 produced units costing 20 yields unit cost 15), production in another branch, and simultaneous repeat production.
14. Verify standard and production bundles coexist in the Bundles list, including older production items with adjustment history. Only production rows have Produce. Check search, pagination, production recipe editing, and deletion guards. From Produce, verify availability loads, excessive quantities disable submission, success returns to Bundles, and the finished product remains sellable as a Product in POS.

Calculation and UI tests do not exercise MySQL transaction behavior or a live POS sale. Those checks require the test tenant above.
