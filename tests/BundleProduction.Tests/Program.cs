using Versa.Core.Services;

static void Equal(decimal expected, decimal actual)
{
    if (expected != actual) throw new Exception($"Expected {expected}, got {actual}");
}
static void Reject(Action action)
{
    try { action(); }
    catch (ArgumentException) { return; }
    throw new Exception("Invalid production was accepted.");
}

var example = BundleProductionCalculation.Combine([new(1, 1, 10, 200, 3)]);
Equal(20, BundleProductionCalculation.MaximumAvailability(example));
BundleProductionCalculation.ValidateQuantity(20, 20);
Equal(0, example[0].Stock - example[0].BaseQuantity * 20);

// Repeated products (including quantities already converted from boxes) share stock.
var repeated = BundleProductionCalculation.Combine([new(1, 1, 10, 200, 3), new(1, 1, 15, 200, 3), new(2, 2, 2, 11, 7)]);
Equal(25, repeated.Single(x => x.ProductId == 1).BaseQuantity);
Equal(5, BundleProductionCalculation.MaximumAvailability(repeated));
Equal(89, repeated.Sum(x => x.BaseQuantity * x.UnitCost));
Equal(75, repeated[0].Stock - repeated[0].BaseQuantity * 5);

Equal(3, BundleProductionCalculation.MaximumAvailability(BundleProductionCalculation.Combine([new(1, 1, .00003m, .0001m, 1)])));
foreach (var stock in new decimal[] { 0, -1, 9 })
    Equal(0, BundleProductionCalculation.MaximumAvailability(BundleProductionCalculation.Combine([new(1, 1, 10, stock, 1)])));
foreach (var quantity in new decimal[] { 0, -1, .5m, 21 })
    Reject(() => BundleProductionCalculation.ValidateQuantity(quantity, 20));
Reject(() => BundleProductionCalculation.ValidateQuantity(20, 19)); // Changed stock at save time.
Reject(() => BundleProductionCalculation.Combine([]));
Reject(() => BundleProductionCalculation.Combine([new(1, 1, 0, 10, 1)]));
Reject(() => BundleProductionCalculation.Combine([new(1, 1, -1, 10, 1)]));
Console.WriteLine("Passed production availability, consumption, costing and validation checks.");
Equal(15, BundleProductionCalculation.FinishedUnitCost(10, 10, 10, 20));
Equal(20, BundleProductionCalculation.FinishedUnitCost(0, 10, 10, 20));
Equal(20, BundleProductionCalculation.FinishedUnitCost(-5, 10, 10, 20));
Reject(() => BundleProductionCalculation.FinishedUnitCost(10, 10, 0, 20));
Console.WriteLine("Passed repeat-production weighted cost checks.");
