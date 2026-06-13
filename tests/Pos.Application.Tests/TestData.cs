using System.Reflection;
using Pos.Domain.Catalog;

namespace Pos.Application.Tests;

public static class TestData
{
    /// <summary>Builds a product and forces its private Id, mimicking a persisted entity.</summary>
    public static Product Product(
        int id, string name = "Brownie", int priceCents = 65, int stock = 10,
        ProductCategory category = ProductCategory.Edible, string imageKey = "brownie", bool isActive = true)
    {
        var product = new Product(name, category, priceCents, stock, imageKey, isActive);
        SetId(product, id);
        return product;
    }

    public static void SetId(object entity, int id)
    {
        var prop = entity.GetType().GetProperty("Id", BindingFlags.Public | BindingFlags.Instance)!;
        prop.SetValue(entity, id, index: null);
    }
}
