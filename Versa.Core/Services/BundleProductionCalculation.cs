namespace Versa.Core.Services;

public record ProductionComponent(int ProductId, int UnitId, decimal BaseQuantity, decimal Stock, decimal UnitCost);

public class BundleProductionException(string code, string message) : ArgumentException(message)
{
    public string Code { get; } = code;
}

public static class BundleProductionCalculation
{
    public static decimal FinishedUnitCost(decimal currentStock, decimal currentCost, decimal producedQuantity, decimal productionUnitCost)
    {
        if (producedQuantity <= 0) throw new BundleProductionException("quantityInvalid", "Production quantity must be positive.");
        var available = Math.Max(0, currentStock);
        return (available * currentCost + producedQuantity * productionUnitCost) / (available + producedQuantity);
    }

    public static List<ProductionComponent> Combine(IEnumerable<ProductionComponent> components)
    {
        var rows = components.ToList();
        if (rows.Count == 0 || rows.Any(x => x.BaseQuantity <= 0))
            throw new BundleProductionException("physicalProductRequired", "Production requires at least one product with a positive component quantity.");

        return rows.GroupBy(x => x.ProductId).Select(group => group.First() with
        {
            BaseQuantity = group.Sum(x => x.BaseQuantity)
        }).OrderBy(x => x.ProductId).ToList();
    }

    public static decimal MaximumAvailability(IReadOnlyCollection<ProductionComponent> components) =>
        components.Count == 0 ? 0 : components.Min(x => decimal.Floor(Math.Max(0, x.Stock) / x.BaseQuantity));

    public static void ValidateQuantity(decimal quantity, decimal maximum)
    {
        if (quantity <= 0 || quantity != decimal.Truncate(quantity))
            throw new BundleProductionException("quantityInvalid", "Production quantity must be a positive whole number.");
        if (quantity > maximum)
            throw new BundleProductionException("insufficientStock", $"Only {maximum:0} units can be produced from the available component stock.");
    }
}
