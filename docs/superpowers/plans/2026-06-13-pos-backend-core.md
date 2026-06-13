# POS Backend Core (Domain + Application) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the pure, fully unit-tested domain and application core of the charity bake-sale POS — entities, change-making, and use-case services — with zero infrastructure dependencies.

**Architecture:** Lightweight Clean Architecture. `Pos.Domain` holds entities, value objects, and the change calculator with no external dependencies. `Pos.Application` holds use-case services that depend only on `Pos.Domain` and on port interfaces (repositories, payment, notifier, unit-of-work) that Plan 2 will implement. All money is integer cents. Time is injected via `TimeProvider` for deterministic tests.

**Tech Stack:** .NET 10, C# 14, xUnit, NSubstitute, `Microsoft.Extensions.TimeProvider.Testing`.

**Plan set:** This is Plan 1 of 4 (Backend Core → Backend Infra+API → Frontend → Compose+Docs). See `docs/superpowers/specs/2026-06-13-pos-system-design.md`.

---

## File Structure

```
PosSystem.sln
src/backend/
  Pos.Domain/
    Catalog/ProductCategory.cs          # enum: Edible, SecondHand
    Catalog/ProductTranslation.cs        # per-culture localized name
    Catalog/Product.cs                   # product aggregate: stock rules, name resolution
    Orders/OrderLine.cs                  # order line with price/name snapshot
    Orders/Order.cs                      # order aggregate: totals + payment invariant
    Payments/ChangePiece.cs              # (denomination, count) record
    Payments/EuroDenominations.cs        # ordered euro denominations in cents
    Payments/ChangeCalculator.cs         # greedy smallest-change
    Exceptions/DomainException.cs        # base
    Exceptions/InsufficientStockException.cs
    Exceptions/InsufficientPaymentException.cs
  Pos.Application/
    Abstractions/IProductRepository.cs
    Abstractions/IOrderRepository.cs
    Abstractions/IUnitOfWork.cs
    Abstractions/IStockNotifier.cs
    Abstractions/IPaymentService.cs
    Abstractions/IReportQueries.cs
    Payments/PaymentResult.cs
    Exceptions/ConcurrencyConflictException.cs
    Exceptions/EmptyCartException.cs
    Exceptions/InvalidQuantityException.cs
    Exceptions/UnknownProductException.cs
    Exceptions/ProductNotFoundException.cs
    Catalog/ProductDto.cs
    Catalog/CatalogService.cs
    Checkout/CheckoutContracts.cs        # CheckoutLine, CheckoutRequest, OrderLineDto, ChangePieceDto, CheckoutResult
    Checkout/CheckoutService.cs
    Reporting/ReportingContracts.cs      # ReportSummaryDto, ItemSoldDto
    Reporting/ReportingService.cs
tests/
  Pos.Domain.Tests/
    ChangeCalculatorTests.cs
    ProductTests.cs
    OrderTests.cs
  Pos.Application.Tests/
    CheckoutServiceTests.cs
    CatalogServiceTests.cs
    ReportingServiceTests.cs
    TestData.cs                          # shared product/order builders
```

Files are split by responsibility (catalog, orders, payments) rather than by technical layer, and kept small so each is easy to hold in context.

---

## Task 1: Solution & project scaffolding

**Files:**
- Create: `PosSystem.sln` and the four projects above.

- [ ] **Step 1: Create solution and projects**

Run from the repo root:

```bash
dotnet new sln -n PosSystem
dotnet new classlib -n Pos.Domain -o src/backend/Pos.Domain -f net10.0
dotnet new classlib -n Pos.Application -o src/backend/Pos.Application -f net10.0
dotnet new xunit -n Pos.Domain.Tests -o tests/Pos.Domain.Tests -f net10.0
dotnet new xunit -n Pos.Application.Tests -o tests/Pos.Application.Tests -f net10.0
```

- [ ] **Step 2: Delete template placeholder files**

```bash
rm src/backend/Pos.Domain/Class1.cs
rm src/backend/Pos.Application/Class1.cs
rm tests/Pos.Domain.Tests/UnitTest1.cs
rm tests/Pos.Application.Tests/UnitTest1.cs
```

- [ ] **Step 3: Wire references and add the solution to it**

```bash
dotnet sln add src/backend/Pos.Domain src/backend/Pos.Application tests/Pos.Domain.Tests tests/Pos.Application.Tests
dotnet add src/backend/Pos.Application reference src/backend/Pos.Domain
dotnet add tests/Pos.Domain.Tests reference src/backend/Pos.Domain
dotnet add tests/Pos.Application.Tests reference src/backend/Pos.Application src/backend/Pos.Domain
dotnet add tests/Pos.Application.Tests package NSubstitute
dotnet add tests/Pos.Application.Tests package Microsoft.Extensions.TimeProvider.Testing
```

- [ ] **Step 4: Confirm `Nullable` and `ImplicitUsings` are enabled**

Each `.csproj` from the templates already contains `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>`. Open all four `.csproj` files and verify both lines are present in the first `<PropertyGroup>`; add any that are missing.

- [ ] **Step 5: Verify the empty solution builds**

Run: `dotnet build PosSystem.sln`
Expected: Build succeeded, 0 errors.

- [ ] **Step 6: Commit**

```bash
git add PosSystem.sln src/ tests/
git commit -m "chore: scaffold backend core solution and test projects"
```

---

## Task 2: Euro denominations & ChangeCalculator (TDD)

**Files:**
- Create: `src/backend/Pos.Domain/Payments/ChangePiece.cs`
- Create: `src/backend/Pos.Domain/Payments/EuroDenominations.cs`
- Create: `src/backend/Pos.Domain/Payments/ChangeCalculator.cs`
- Test: `tests/Pos.Domain.Tests/ChangeCalculatorTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/Pos.Domain.Tests/ChangeCalculatorTests.cs`:

```csharp
using Pos.Domain.Payments;

namespace Pos.Domain.Tests;

public class ChangeCalculatorTests
{
    [Fact]
    public void Calculate_ZeroChange_ReturnsNoPieces()
    {
        var pieces = ChangeCalculator.Calculate(0);
        Assert.Empty(pieces);
    }

    [Fact]
    public void Calculate_270Cents_ReturnsFewestPieces()
    {
        // €2.70 = 1×200 + 1×50 + 1×20
        var pieces = ChangeCalculator.Calculate(270);

        Assert.Equal(
            new[] { new ChangePiece(200, 1), new ChangePiece(50, 1), new ChangePiece(20, 1) },
            pieces);
    }

    [Fact]
    public void Calculate_99Cents_UsesCoinsGreedily()
    {
        // 99c = 50 + 20 + 20 + 5 + 2 + 2
        var pieces = ChangeCalculator.Calculate(99);

        Assert.Equal(
            new[]
            {
                new ChangePiece(50, 1), new ChangePiece(20, 2),
                new ChangePiece(5, 1), new ChangePiece(2, 2)
            },
            pieces);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(270)]
    [InlineData(9999)]
    [InlineData(123_45)]
    public void Calculate_PiecesSumBackToAmount(int amount)
    {
        var total = ChangeCalculator.Calculate(amount).Sum(p => p.DenominationCents * p.Count);
        Assert.Equal(amount, total);
    }

    [Fact]
    public void Calculate_EveryAmountUpTo1000_SumsBack()
    {
        for (var amount = 0; amount <= 1000; amount++)
        {
            var total = ChangeCalculator.Calculate(amount).Sum(p => p.DenominationCents * p.Count);
            Assert.Equal(amount, total);
        }
    }

    [Fact]
    public void Calculate_NegativeAmount_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ChangeCalculator.Calculate(-1));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Pos.Domain.Tests --filter ChangeCalculatorTests`
Expected: FAIL — `ChangePiece`/`ChangeCalculator` do not exist (compile error).

- [ ] **Step 3: Implement ChangePiece**

Create `src/backend/Pos.Domain/Payments/ChangePiece.cs`:

```csharp
namespace Pos.Domain.Payments;

/// <summary>A quantity of a single denomination, e.g. 2 × 50c.</summary>
public sealed record ChangePiece(int DenominationCents, int Count);
```

- [ ] **Step 4: Implement EuroDenominations**

Create `src/backend/Pos.Domain/Payments/EuroDenominations.cs`:

```csharp
namespace Pos.Domain.Payments;

/// <summary>Standard euro denominations in cents, descending (notes then coins).</summary>
public static class EuroDenominations
{
    public static readonly IReadOnlyList<int> InCents =
    [
        50_000, 20_000, 10_000, 5_000, 2_000, 1_000, // €500..€10 notes
        500, 200, 100,                                // €5, €2, €1
        50, 20, 10, 5, 2, 1                           // 50c..1c coins
    ];
}
```

- [ ] **Step 5: Implement ChangeCalculator**

Create `src/backend/Pos.Domain/Payments/ChangeCalculator.cs`:

```csharp
namespace Pos.Domain.Payments;

/// <summary>
/// Computes the smallest number of physical pieces for a change amount.
/// Greedy is provably optimal for the canonical euro denomination set, and the
/// drawer is assumed to hold an unlimited supply of every denomination.
/// </summary>
public static class ChangeCalculator
{
    public static IReadOnlyList<ChangePiece> Calculate(int changeCents)
    {
        if (changeCents < 0)
            throw new ArgumentOutOfRangeException(nameof(changeCents), "Change cannot be negative.");

        var pieces = new List<ChangePiece>();
        var remaining = changeCents;

        foreach (var denomination in EuroDenominations.InCents)
        {
            if (remaining < denomination)
                continue;

            var count = remaining / denomination;
            remaining -= count * denomination;
            pieces.Add(new ChangePiece(denomination, count));
        }

        return pieces;
    }
}
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test tests/Pos.Domain.Tests --filter ChangeCalculatorTests`
Expected: PASS — all ChangeCalculator tests green.

- [ ] **Step 7: Commit**

```bash
git add src/backend/Pos.Domain/Payments tests/Pos.Domain.Tests/ChangeCalculatorTests.cs
git commit -m "feat(domain): add greedy euro change calculator"
```

---

## Task 3: Domain exceptions

**Files:**
- Create: `src/backend/Pos.Domain/Exceptions/DomainException.cs`
- Create: `src/backend/Pos.Domain/Exceptions/InsufficientStockException.cs`
- Create: `src/backend/Pos.Domain/Exceptions/InsufficientPaymentException.cs`

These have no behavior to test alone; they are exercised by Product and Order tests in later tasks.

- [ ] **Step 1: Implement the base exception**

Create `src/backend/Pos.Domain/Exceptions/DomainException.cs`:

```csharp
namespace Pos.Domain.Exceptions;

/// <summary>Base type for violations of domain invariants.</summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
}
```

- [ ] **Step 2: Implement InsufficientStockException**

Create `src/backend/Pos.Domain/Exceptions/InsufficientStockException.cs`:

```csharp
namespace Pos.Domain.Exceptions;

public sealed class InsufficientStockException : DomainException
{
    public int ProductId { get; }
    public int Requested { get; }
    public int Available { get; }

    public InsufficientStockException(int productId, int requested, int available)
        : base($"Insufficient stock for product {productId}: requested {requested}, available {available}.")
    {
        ProductId = productId;
        Requested = requested;
        Available = available;
    }
}
```

- [ ] **Step 3: Implement InsufficientPaymentException**

Create `src/backend/Pos.Domain/Exceptions/InsufficientPaymentException.cs`:

```csharp
namespace Pos.Domain.Exceptions;

public sealed class InsufficientPaymentException : DomainException
{
    public int TotalCents { get; }
    public int CashPaidCents { get; }

    public InsufficientPaymentException(int totalCents, int cashPaidCents)
        : base($"Insufficient payment: total {totalCents}c, paid {cashPaidCents}c.")
    {
        TotalCents = totalCents;
        CashPaidCents = cashPaidCents;
    }
}
```

- [ ] **Step 4: Build to verify it compiles**

Run: `dotnet build src/backend/Pos.Domain`
Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add src/backend/Pos.Domain/Exceptions
git commit -m "feat(domain): add domain exception types"
```

---

## Task 4: Product & ProductTranslation (TDD)

**Files:**
- Create: `src/backend/Pos.Domain/Catalog/ProductCategory.cs`
- Create: `src/backend/Pos.Domain/Catalog/ProductTranslation.cs`
- Create: `src/backend/Pos.Domain/Catalog/Product.cs`
- Test: `tests/Pos.Domain.Tests/ProductTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/Pos.Domain.Tests/ProductTests.cs`:

```csharp
using Pos.Domain.Catalog;
using Pos.Domain.Exceptions;

namespace Pos.Domain.Tests;

public class ProductTests
{
    private static Product NewBrownie(int stock = 10) =>
        new("Brownie", ProductCategory.Edible, priceCents: 65, stockQuantity: stock, imageKey: "brownie");

    [Fact]
    public void Constructor_WithBlankName_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new Product(" ", ProductCategory.Edible, 65, 10, "brownie"));
    }

    [Fact]
    public void Constructor_WithNegativeStock_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Product("Brownie", ProductCategory.Edible, 65, -1, "brownie"));
    }

    [Fact]
    public void IsOutOfStock_WhenZero_IsTrue()
    {
        Assert.True(NewBrownie(stock: 0).IsOutOfStock);
        Assert.False(NewBrownie(stock: 1).IsOutOfStock);
    }

    [Fact]
    public void GetName_WithNoTranslation_FallsBackToCanonical()
    {
        var product = NewBrownie();
        Assert.Equal("Brownie", product.GetName("et"));
        Assert.Equal("Brownie", product.GetName(null));
    }

    [Fact]
    public void GetName_WithTranslation_ReturnsLocalizedName()
    {
        var product = NewBrownie();
        product.AddTranslation("et", "Brauni");

        Assert.Equal("Brauni", product.GetName("et"));
        Assert.Equal("Brauni", product.GetName("ET")); // case-insensitive
        Assert.Equal("Brownie", product.GetName("en")); // no en translation -> canonical
    }

    [Fact]
    public void AddTranslation_DuplicateCulture_Throws()
    {
        var product = NewBrownie();
        product.AddTranslation("et", "Brauni");
        Assert.Throws<InvalidOperationException>(() => product.AddTranslation("et", "Muu"));
    }

    [Fact]
    public void HasSufficientStock_RespectsQuantityAndActiveFlag()
    {
        var product = NewBrownie(stock: 3);
        Assert.True(product.HasSufficientStock(3));
        Assert.False(product.HasSufficientStock(4));
        Assert.False(product.HasSufficientStock(0));
    }

    [Fact]
    public void DecreaseStock_ReducesQuantity()
    {
        var product = NewBrownie(stock: 5);
        product.DecreaseStock(2);
        Assert.Equal(3, product.StockQuantity);
    }

    [Fact]
    public void DecreaseStock_BeyondAvailable_ThrowsInsufficientStock()
    {
        var product = NewBrownie(stock: 1);
        var ex = Assert.Throws<InsufficientStockException>(() => product.DecreaseStock(2));
        Assert.Equal(2, ex.Requested);
        Assert.Equal(1, ex.Available);
    }

    [Fact]
    public void SetStock_Negative_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NewBrownie().SetStock(-1));
    }

    [Fact]
    public void SetStock_SetsAbsoluteQuantity()
    {
        var product = NewBrownie(stock: 0);
        product.SetStock(25);
        Assert.Equal(25, product.StockQuantity);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Pos.Domain.Tests --filter ProductTests`
Expected: FAIL — `Product`/`ProductCategory`/`ProductTranslation` do not exist.

- [ ] **Step 3: Implement ProductCategory**

Create `src/backend/Pos.Domain/Catalog/ProductCategory.cs`:

```csharp
namespace Pos.Domain.Catalog;

public enum ProductCategory
{
    Edible,
    SecondHand
}
```

- [ ] **Step 4: Implement ProductTranslation**

Create `src/backend/Pos.Domain/Catalog/ProductTranslation.cs`:

```csharp
namespace Pos.Domain.Catalog;

/// <summary>A localized product name for one neutral culture (e.g. "et").</summary>
public class ProductTranslation
{
    public int Id { get; private set; }
    public int ProductId { get; private set; }
    public string CultureCode { get; private set; } = null!;
    public string Name { get; private set; } = null!;

    private ProductTranslation() { } // EF

    public ProductTranslation(string cultureCode, string name)
    {
        if (string.IsNullOrWhiteSpace(cultureCode))
            throw new ArgumentException("Culture code is required.", nameof(cultureCode));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));

        CultureCode = cultureCode.ToLowerInvariant();
        Name = name;
    }
}
```

- [ ] **Step 5: Implement Product**

Create `src/backend/Pos.Domain/Catalog/Product.cs`:

```csharp
using Pos.Domain.Exceptions;

namespace Pos.Domain.Catalog;

/// <summary>Catalog product. Canonical <see cref="Name"/> is the base-culture (English) name;
/// per-culture overrides live in <see cref="Translations"/> with fallback to the canonical name.</summary>
public class Product
{
    private readonly List<ProductTranslation> _translations = [];

    public int Id { get; private set; }
    public string Name { get; private set; } = null!;
    public ProductCategory Category { get; private set; }
    public int PriceCents { get; private set; }
    public int StockQuantity { get; private set; }
    public string ImageKey { get; private set; } = null!;
    public bool IsActive { get; private set; }

    public IReadOnlyCollection<ProductTranslation> Translations => _translations;
    public bool IsOutOfStock => StockQuantity <= 0;

    private Product() { } // EF

    public Product(
        string name, ProductCategory category, int priceCents,
        int stockQuantity, string imageKey, bool isActive = true)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        if (priceCents < 0)
            throw new ArgumentOutOfRangeException(nameof(priceCents), "Price cannot be negative.");
        if (stockQuantity < 0)
            throw new ArgumentOutOfRangeException(nameof(stockQuantity), "Stock cannot be negative.");
        if (string.IsNullOrWhiteSpace(imageKey))
            throw new ArgumentException("Image key is required.", nameof(imageKey));

        Name = name;
        Category = category;
        PriceCents = priceCents;
        StockQuantity = stockQuantity;
        ImageKey = imageKey;
        IsActive = isActive;
    }

    public void AddTranslation(string cultureCode, string name)
    {
        var code = cultureCode.ToLowerInvariant();
        if (_translations.Any(t => t.CultureCode == code))
            throw new InvalidOperationException($"Translation for '{code}' already exists.");
        _translations.Add(new ProductTranslation(code, name));
    }

    /// <summary>Resolves the display name for a neutral culture, falling back to the canonical name.</summary>
    public string GetName(string? cultureCode)
    {
        if (string.IsNullOrWhiteSpace(cultureCode))
            return Name;
        var code = cultureCode.ToLowerInvariant();
        return _translations.FirstOrDefault(t => t.CultureCode == code)?.Name ?? Name;
    }

    public bool HasSufficientStock(int quantity)
        => IsActive && quantity > 0 && StockQuantity >= quantity;

    public void DecreaseStock(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        if (StockQuantity < quantity)
            throw new InsufficientStockException(Id, quantity, StockQuantity);
        StockQuantity -= quantity;
    }

    public void SetStock(int quantity)
    {
        if (quantity < 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Stock cannot be negative.");
        StockQuantity = quantity;
    }
}
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test tests/Pos.Domain.Tests --filter ProductTests`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src/backend/Pos.Domain/Catalog tests/Pos.Domain.Tests/ProductTests.cs
git commit -m "feat(domain): add product aggregate with localized names and stock rules"
```

---

## Task 5: Order & OrderLine (TDD)

**Files:**
- Create: `src/backend/Pos.Domain/Orders/OrderLine.cs`
- Create: `src/backend/Pos.Domain/Orders/Order.cs`
- Test: `tests/Pos.Domain.Tests/OrderTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/Pos.Domain.Tests/OrderTests.cs`:

```csharp
using Pos.Domain.Exceptions;
using Pos.Domain.Orders;

namespace Pos.Domain.Tests;

public class OrderTests
{
    private static readonly DateTime At = new(2026, 6, 13, 10, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Key = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void OrderLine_ComputesLineTotal()
    {
        var line = new OrderLine(productId: 1, productName: "Brownie", unitPriceCents: 65, quantity: 3);
        Assert.Equal(195, line.LineTotalCents);
    }

    [Fact]
    public void OrderLine_NonPositiveQuantity_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new OrderLine(1, "Brownie", 65, 0));
    }

    [Fact]
    public void Create_SumsLineTotalsAndStoresChange()
    {
        var lines = new[]
        {
            new OrderLine(1, "Brownie", 65, 2),  // 130
            new OrderLine(2, "Muffin", 100, 1),  // 100
        };

        var order = Order.Create(lines, cashPaidCents: 500, changeCents: 270, idempotencyKey: Key, createdAtUtc: At);

        Assert.Equal(230, order.TotalCents);
        Assert.Equal(500, order.CashPaidCents);
        Assert.Equal(270, order.ChangeCents);
        Assert.Equal(Key, order.IdempotencyKey);
        Assert.Equal(At, order.CreatedAtUtc);
        Assert.Equal(2, order.Lines.Count);
    }

    [Fact]
    public void Create_WithNoLines_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            Order.Create([], 0, 0, Key, At));
    }

    [Fact]
    public void Create_WhenCashBelowTotal_ThrowsInsufficientPayment()
    {
        var lines = new[] { new OrderLine(1, "Brownie", 65, 2) }; // 130
        Assert.Throws<InsufficientPaymentException>(() =>
            Order.Create(lines, cashPaidCents: 100, changeCents: 0, idempotencyKey: Key, createdAtUtc: At));
    }

    [Fact]
    public void Create_WhenChangeInconsistent_Throws()
    {
        var lines = new[] { new OrderLine(1, "Brownie", 65, 2) }; // 130, cash 200 -> change must be 70
        Assert.Throws<ArgumentException>(() =>
            Order.Create(lines, cashPaidCents: 200, changeCents: 99, idempotencyKey: Key, createdAtUtc: At));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Pos.Domain.Tests --filter OrderTests`
Expected: FAIL — `Order`/`OrderLine` do not exist.

- [ ] **Step 3: Implement OrderLine**

Create `src/backend/Pos.Domain/Orders/OrderLine.cs`:

```csharp
namespace Pos.Domain.Orders;

/// <summary>A purchased line. Name and unit price are snapshotted at sale time so historical
/// orders stay correct if the product is later renamed or repriced.</summary>
public class OrderLine
{
    public int Id { get; private set; }
    public int OrderId { get; private set; }
    public int ProductId { get; private set; }
    public string ProductName { get; private set; } = null!;
    public int UnitPriceCents { get; private set; }
    public int Quantity { get; private set; }

    public int LineTotalCents => UnitPriceCents * Quantity;

    private OrderLine() { } // EF

    public OrderLine(int productId, string productName, int unitPriceCents, int quantity)
    {
        if (string.IsNullOrWhiteSpace(productName))
            throw new ArgumentException("Product name is required.", nameof(productName));
        if (unitPriceCents < 0)
            throw new ArgumentOutOfRangeException(nameof(unitPriceCents), "Price cannot be negative.");
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");

        ProductId = productId;
        ProductName = productName;
        UnitPriceCents = unitPriceCents;
        Quantity = quantity;
    }
}
```

- [ ] **Step 4: Implement Order**

Create `src/backend/Pos.Domain/Orders/Order.cs`:

```csharp
using Pos.Domain.Exceptions;

namespace Pos.Domain.Orders;

/// <summary>A completed sale. Created via <see cref="Create"/>; the canonical price total is
/// derived from the lines and the payment invariant (cash ≥ total) is enforced here.</summary>
public class Order
{
    private readonly List<OrderLine> _lines = [];

    public int Id { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public int TotalCents { get; private set; }
    public int CashPaidCents { get; private set; }
    public int ChangeCents { get; private set; }
    public Guid IdempotencyKey { get; private set; }

    public IReadOnlyCollection<OrderLine> Lines => _lines;

    private Order() { } // EF

    private Order(
        IEnumerable<OrderLine> lines, int cashPaidCents, int changeCents,
        Guid idempotencyKey, DateTime createdAtUtc)
    {
        _lines.AddRange(lines);
        if (_lines.Count == 0)
            throw new ArgumentException("Order must have at least one line.", nameof(lines));

        TotalCents = _lines.Sum(l => l.LineTotalCents);
        if (cashPaidCents < TotalCents)
            throw new InsufficientPaymentException(TotalCents, cashPaidCents);
        if (changeCents != cashPaidCents - TotalCents)
            throw new ArgumentException("Change must equal cash paid minus total.", nameof(changeCents));

        CashPaidCents = cashPaidCents;
        ChangeCents = changeCents;
        IdempotencyKey = idempotencyKey;
        CreatedAtUtc = createdAtUtc;
    }

    public static Order Create(
        IEnumerable<OrderLine> lines, int cashPaidCents, int changeCents,
        Guid idempotencyKey, DateTime createdAtUtc)
        => new(lines, cashPaidCents, changeCents, idempotencyKey, createdAtUtc);
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/Pos.Domain.Tests --filter OrderTests`
Expected: PASS.

- [ ] **Step 6: Run the full domain test suite**

Run: `dotnet test tests/Pos.Domain.Tests`
Expected: PASS — all domain tests green.

- [ ] **Step 7: Commit**

```bash
git add src/backend/Pos.Domain/Orders tests/Pos.Domain.Tests/OrderTests.cs
git commit -m "feat(domain): add order aggregate with payment invariant"
```

---

## Task 6: Application ports, DTOs & exceptions

These are contracts with no behavior; they are exercised by the service tests in Tasks 7–9. No TDD cycle — create, build, commit.

**Files:**
- Create: all files under `src/backend/Pos.Application/Abstractions/`, `Payments/`, `Exceptions/`, plus the DTO containers.

- [ ] **Step 1: Implement PaymentResult**

Create `src/backend/Pos.Application/Payments/PaymentResult.cs`:

```csharp
using Pos.Domain.Payments;

namespace Pos.Application.Payments;

/// <summary>Outcome of accepting cash: the change owed and its denomination breakdown.</summary>
public sealed record PaymentResult(int ChangeCents, IReadOnlyList<ChangePiece> Breakdown);
```

- [ ] **Step 2: Implement the port interfaces**

Create `src/backend/Pos.Application/Abstractions/IProductRepository.cs`:

```csharp
using Pos.Domain.Catalog;

namespace Pos.Application.Abstractions;

public interface IProductRepository
{
    Task<IReadOnlyList<Product>> GetAllActiveAsync(CancellationToken ct = default);
    Task<Product?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<Product>> GetByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct = default);
}
```

Create `src/backend/Pos.Application/Abstractions/IOrderRepository.cs`:

```csharp
using Pos.Domain.Orders;

namespace Pos.Application.Abstractions;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Order?> GetByIdempotencyKeyAsync(Guid key, CancellationToken ct = default);
    Task AddAsync(Order order, CancellationToken ct = default);
}
```

Create `src/backend/Pos.Application/Abstractions/IUnitOfWork.cs`:

```csharp
namespace Pos.Application.Abstractions;

public interface IUnitOfWork
{
    /// <summary>Persists pending changes. Implementations MUST translate an optimistic-concurrency
    /// failure into <see cref="Pos.Application.Exceptions.ConcurrencyConflictException"/>.</summary>
    Task SaveChangesAsync(CancellationToken ct = default);
}
```

Create `src/backend/Pos.Application/Abstractions/IStockNotifier.cs`:

```csharp
namespace Pos.Application.Abstractions;

public interface IStockNotifier
{
    Task NotifyStockChangedAsync(int productId, int newQuantity, CancellationToken ct = default);
}
```

Create `src/backend/Pos.Application/Abstractions/IPaymentService.cs`:

```csharp
using Pos.Application.Payments;

namespace Pos.Application.Abstractions;

public interface IPaymentService
{
    /// <summary>Accepts cash for a total and returns the change owed. MUST throw
    /// <see cref="Pos.Domain.Exceptions.InsufficientPaymentException"/> when cash is short.</summary>
    PaymentResult AcceptCash(int totalCents, int cashPaidCents);
}
```

Create `src/backend/Pos.Application/Abstractions/IReportQueries.cs`:

```csharp
namespace Pos.Application.Abstractions;

public interface IReportQueries
{
    Task<ReportTotals> GetTotalsAsync(CancellationToken ct = default);
}

public sealed record ReportTotals(int TotalFundsCents, int OrderCount, IReadOnlyList<ItemSold> Items);

public sealed record ItemSold(int ProductId, int QuantitySold, int RevenueCents);
```

- [ ] **Step 3: Implement application exceptions**

Create `src/backend/Pos.Application/Exceptions/ConcurrencyConflictException.cs`:

```csharp
namespace Pos.Application.Exceptions;

/// <summary>Raised when a persistence optimistic-concurrency conflict cannot be resolved.</summary>
public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(string message = "A concurrency conflict occurred.")
        : base(message) { }
}
```

Create `src/backend/Pos.Application/Exceptions/EmptyCartException.cs`:

```csharp
namespace Pos.Application.Exceptions;

public sealed class EmptyCartException : Exception
{
    public EmptyCartException() : base("The cart is empty.") { }
}
```

Create `src/backend/Pos.Application/Exceptions/InvalidQuantityException.cs`:

```csharp
namespace Pos.Application.Exceptions;

public sealed class InvalidQuantityException : Exception
{
    public int ProductId { get; }
    public int Quantity { get; }

    public InvalidQuantityException(int productId, int quantity)
        : base($"Invalid quantity {quantity} for product {productId}.")
    {
        ProductId = productId;
        Quantity = quantity;
    }
}
```

Create `src/backend/Pos.Application/Exceptions/UnknownProductException.cs`:

```csharp
namespace Pos.Application.Exceptions;

public sealed class UnknownProductException : Exception
{
    public int ProductId { get; }

    public UnknownProductException(int productId)
        : base($"Unknown product {productId}.")
    {
        ProductId = productId;
    }
}
```

Create `src/backend/Pos.Application/Exceptions/ProductNotFoundException.cs`:

```csharp
namespace Pos.Application.Exceptions;

public sealed class ProductNotFoundException : Exception
{
    public int ProductId { get; }

    public ProductNotFoundException(int productId)
        : base($"Product {productId} was not found.")
    {
        ProductId = productId;
    }
}
```

- [ ] **Step 4: Implement DTO containers**

Create `src/backend/Pos.Application/Catalog/ProductDto.cs`:

```csharp
namespace Pos.Application.Catalog;

public sealed record ProductDto(
    int Id, string Name, string Category, int PriceCents,
    int StockQuantity, string ImageKey, bool IsOutOfStock);
```

Create `src/backend/Pos.Application/Checkout/CheckoutContracts.cs`:

```csharp
namespace Pos.Application.Checkout;

public sealed record CheckoutLine(int ProductId, int Quantity);

public sealed record CheckoutRequest(
    IReadOnlyList<CheckoutLine> Lines, int CashPaidCents, Guid IdempotencyKey);

public sealed record OrderLineDto(
    int ProductId, string ProductName, int UnitPriceCents, int Quantity, int LineTotalCents);

public sealed record ChangePieceDto(int DenominationCents, int Count);

public sealed record CheckoutResult(
    int OrderId, int TotalCents, int CashPaidCents, int ChangeCents,
    IReadOnlyList<ChangePieceDto> Change, IReadOnlyList<OrderLineDto> Lines, DateTime CreatedAtUtc);
```

Create `src/backend/Pos.Application/Reporting/ReportingContracts.cs`:

```csharp
namespace Pos.Application.Reporting;

public sealed record ReportSummaryDto(int TotalFundsCents, int OrderCount, IReadOnlyList<ItemSoldDto> Items);

public sealed record ItemSoldDto(int ProductId, string Name, int QuantitySold, int RevenueCents);
```

- [ ] **Step 5: Build to verify it compiles**

Run: `dotnet build src/backend/Pos.Application`
Expected: Build succeeded.

- [ ] **Step 6: Commit**

```bash
git add src/backend/Pos.Application
git commit -m "feat(application): add ports, DTOs and application exceptions"
```

---

## Task 7: Shared test data builder

**Files:**
- Create: `tests/Pos.Application.Tests/TestData.cs`

- [ ] **Step 1: Implement the builder**

Create `tests/Pos.Application.Tests/TestData.cs`. This centralizes product construction and sets ids via reflection (the domain keeps setters private), so service tests can refer to stable ids.

```csharp
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
```

Note: `Id` has a private setter, so `SetValue` works through the public property's non-public setter via reflection (BindingFlags above resolve the property; the setter accessibility is bypassed by reflection).

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build tests/Pos.Application.Tests`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add tests/Pos.Application.Tests/TestData.cs
git commit -m "test(application): add shared product test data builder"
```

---

## Task 8: CheckoutService (TDD)

This is the heart of the system — orchestration, validation, idempotency, and concurrency retry.

**Files:**
- Create: `src/backend/Pos.Application/Checkout/CheckoutService.cs`
- Test: `tests/Pos.Application.Tests/CheckoutServiceTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/Pos.Application.Tests/CheckoutServiceTests.cs`:

```csharp
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Pos.Application.Abstractions;
using Pos.Application.Checkout;
using Pos.Application.Exceptions;
using Pos.Application.Payments;
using Pos.Domain.Exceptions;
using Pos.Domain.Orders;
using Pos.Domain.Payments;

namespace Pos.Application.Tests;

public class CheckoutServiceTests
{
    private readonly IProductRepository _products = Substitute.For<IProductRepository>();
    private readonly IOrderRepository _orders = Substitute.For<IOrderRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IPaymentService _payment = Substitute.For<IPaymentService>();
    private readonly IStockNotifier _notifier = Substitute.For<IStockNotifier>();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 6, 13, 10, 0, 0, TimeSpan.Zero));

    private CheckoutService CreateSut() => new(_products, _orders, _uow, _payment, _notifier, _clock);

    private void RealisticPayment() =>
        _payment.AcceptCash(Arg.Any<int>(), Arg.Any<int>()).Returns(ci =>
        {
            int total = ci.ArgAt<int>(0), cash = ci.ArgAt<int>(1);
            if (cash < total) throw new InsufficientPaymentException(total, cash);
            var change = cash - total;
            return new PaymentResult(change, ChangeCalculator.Calculate(change));
        });

    private static CheckoutRequest Request(int cash, params (int productId, int qty)[] lines) =>
        new(lines.Select(l => new CheckoutLine(l.productId, l.qty)).ToList(), cash, Guid.NewGuid());

    [Fact]
    public async Task Checkout_WithExactPayment_ReturnsZeroChange()
    {
        RealisticPayment();
        _products.GetByIdsAsync(Arg.Any<IReadOnlyCollection<int>>())
            .Returns([TestData.Product(1, "Brownie", priceCents: 65, stock: 10)]);

        var result = await CreateSut().CheckoutAsync(Request(cash: 130, (1, 2)));

        Assert.Equal(130, result.TotalCents);
        Assert.Equal(0, result.ChangeCents);
        Assert.Empty(result.Change);
        Assert.Equal(2, result.Lines.Single().Quantity);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Checkout_WithOverpayment_ReturnsChangeBreakdown()
    {
        RealisticPayment();
        _products.GetByIdsAsync(Arg.Any<IReadOnlyCollection<int>>())
            .Returns([TestData.Product(1, "Brownie", priceCents: 65, stock: 10)]);

        var result = await CreateSut().CheckoutAsync(Request(cash: 200, (1, 2))); // total 130, change 70

        Assert.Equal(70, result.ChangeCents);
        Assert.Equal(new ChangePieceDto(50, 1), result.Change[0]);
        Assert.Equal(new ChangePieceDto(20, 1), result.Change[1]);
    }

    [Fact]
    public async Task Checkout_MergesDuplicateLinesForSameProduct()
    {
        RealisticPayment();
        _products.GetByIdsAsync(Arg.Any<IReadOnlyCollection<int>>())
            .Returns([TestData.Product(1, "Brownie", priceCents: 65, stock: 10)]);

        // Two separate clicks on the same product arrive as two lines.
        var result = await CreateSut().CheckoutAsync(Request(cash: 195, (1, 1), (1, 2)));

        Assert.Equal(195, result.TotalCents);
        Assert.Equal(3, result.Lines.Single().Quantity);
    }

    [Fact]
    public async Task Checkout_WhenStockInsufficient_ThrowsInsufficientStock()
    {
        RealisticPayment();
        _products.GetByIdsAsync(Arg.Any<IReadOnlyCollection<int>>())
            .Returns([TestData.Product(1, "Brownie", priceCents: 65, stock: 1)]);

        await Assert.ThrowsAsync<InsufficientStockException>(() =>
            CreateSut().CheckoutAsync(Request(cash: 130, (1, 2))));

        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Checkout_WhenCashTooLow_ThrowsInsufficientPaymentAndDoesNotPersist()
    {
        RealisticPayment();
        _products.GetByIdsAsync(Arg.Any<IReadOnlyCollection<int>>())
            .Returns([TestData.Product(1, "Brownie", priceCents: 65, stock: 10)]);

        await Assert.ThrowsAsync<InsufficientPaymentException>(() =>
            CreateSut().CheckoutAsync(Request(cash: 100, (1, 2)))); // total 130

        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Checkout_WithEmptyCart_ThrowsEmptyCart()
    {
        var request = new CheckoutRequest([], 0, Guid.NewGuid());
        await Assert.ThrowsAsync<EmptyCartException>(() => CreateSut().CheckoutAsync(request));
    }

    [Fact]
    public async Task Checkout_WithZeroQuantity_ThrowsInvalidQuantity()
    {
        await Assert.ThrowsAsync<InvalidQuantityException>(() =>
            CreateSut().CheckoutAsync(Request(cash: 100, (1, 0))));
    }

    [Fact]
    public async Task Checkout_WithUnknownProduct_ThrowsUnknownProduct()
    {
        RealisticPayment();
        _products.GetByIdsAsync(Arg.Any<IReadOnlyCollection<int>>())
            .Returns([]); // requested product not returned

        await Assert.ThrowsAsync<UnknownProductException>(() =>
            CreateSut().CheckoutAsync(Request(cash: 100, (99, 1))));
    }

    [Fact]
    public async Task Checkout_DecrementsStockAndNotifies()
    {
        RealisticPayment();
        var product = TestData.Product(1, "Brownie", priceCents: 65, stock: 10);
        _products.GetByIdsAsync(Arg.Any<IReadOnlyCollection<int>>()).Returns([product]);

        await CreateSut().CheckoutAsync(Request(cash: 130, (1, 2)));

        Assert.Equal(8, product.StockQuantity);
        await _notifier.Received(1).NotifyStockChangedAsync(1, 8, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Checkout_WithExistingIdempotencyKey_ReturnsExistingWithoutCharging()
    {
        var key = Guid.NewGuid();
        var existing = Order.Create(
            [new OrderLine(1, "Brownie", 65, 2)], cashPaidCents: 200, changeCents: 70,
            idempotencyKey: key, createdAtUtc: _clock.GetUtcNow().UtcDateTime);
        TestData.SetId(existing, 555);
        _orders.GetByIdempotencyKeyAsync(key).Returns(existing);

        var request = new CheckoutRequest([new CheckoutLine(1, 2)], 200, key);
        var result = await CreateSut().CheckoutAsync(request);

        Assert.Equal(555, result.OrderId);
        Assert.Equal(70, result.ChangeCents);
        Assert.Equal(new ChangePieceDto(50, 1), result.Change[0]); // breakdown recomputed deterministically
        await _products.DidNotReceive().GetByIdsAsync(Arg.Any<IReadOnlyCollection<int>>());
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Checkout_OnConcurrencyConflict_RetriesOnceThenSucceeds()
    {
        RealisticPayment();
        // Fresh product instance returned on each load so the retry re-validates against current state.
        _products.GetByIdsAsync(Arg.Any<IReadOnlyCollection<int>>())
            .Returns(_ => [TestData.Product(1, "Brownie", priceCents: 65, stock: 10)]);
        _uow.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(_ => throw new ConcurrencyConflictException(), _ => Task.CompletedTask);

        var result = await CreateSut().CheckoutAsync(Request(cash: 130, (1, 2)));

        Assert.Equal(130, result.TotalCents);
        await _uow.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Checkout_WhenConflictPersists_Throws()
    {
        RealisticPayment();
        _products.GetByIdsAsync(Arg.Any<IReadOnlyCollection<int>>())
            .Returns(_ => [TestData.Product(1, "Brownie", priceCents: 65, stock: 10)]);
        _uow.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Throws(new ConcurrencyConflictException());

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            CreateSut().CheckoutAsync(Request(cash: 130, (1, 2))));

        await _uow.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Pos.Application.Tests --filter CheckoutServiceTests`
Expected: FAIL — `CheckoutService` does not exist.

- [ ] **Step 3: Implement CheckoutService**

Create `src/backend/Pos.Application/Checkout/CheckoutService.cs`:

```csharp
using Pos.Application.Abstractions;
using Pos.Application.Exceptions;
using Pos.Domain.Exceptions;
using Pos.Domain.Orders;
using Pos.Domain.Payments;

namespace Pos.Application.Checkout;

/// <summary>Orchestrates a checkout: validation, payment, transactional stock decrement with a
/// single optimistic-concurrency retry, persistence, and live stock notification.</summary>
public sealed class CheckoutService
{
    private const int MaxAttempts = 2; // initial attempt + one retry on a concurrency conflict

    private readonly IProductRepository _products;
    private readonly IOrderRepository _orders;
    private readonly IUnitOfWork _uow;
    private readonly IPaymentService _payment;
    private readonly IStockNotifier _notifier;
    private readonly TimeProvider _clock;

    public CheckoutService(
        IProductRepository products, IOrderRepository orders, IUnitOfWork uow,
        IPaymentService payment, IStockNotifier notifier, TimeProvider clock)
    {
        _products = products;
        _orders = orders;
        _uow = uow;
        _payment = payment;
        _notifier = notifier;
        _clock = clock;
    }

    public async Task<CheckoutResult> CheckoutAsync(CheckoutRequest request, CancellationToken ct = default)
    {
        if (request.Lines is null || request.Lines.Count == 0)
            throw new EmptyCartException();
        foreach (var line in request.Lines)
            if (line.Quantity <= 0)
                throw new InvalidQuantityException(line.ProductId, line.Quantity);

        var existing = await _orders.GetByIdempotencyKeyAsync(request.IdempotencyKey, ct);
        if (existing is not null)
            return Map(existing);

        // Multiple clicks on one product may arrive as separate lines; consolidate by product.
        var quantities = request.Lines
            .GroupBy(l => l.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await ProcessAsync(quantities, request, ct);
            }
            catch (ConcurrencyConflictException) when (attempt < MaxAttempts)
            {
                // Reload-and-retry: the next ProcessAsync loads fresh product state.
            }
        }
    }

    private async Task<CheckoutResult> ProcessAsync(
        IReadOnlyDictionary<int, int> quantities, CheckoutRequest request, CancellationToken ct)
    {
        var products = await _products.GetByIdsAsync(quantities.Keys.ToArray(), ct);
        var byId = products.ToDictionary(p => p.Id);

        foreach (var productId in quantities.Keys)
            if (!byId.ContainsKey(productId))
                throw new UnknownProductException(productId);

        // Validate availability before any mutation or payment.
        foreach (var (productId, quantity) in quantities)
            if (!byId[productId].HasSufficientStock(quantity))
                throw new InsufficientStockException(productId, quantity, byId[productId].StockQuantity);

        var lines = quantities
            .Select(kv => new OrderLine(kv.Key, byId[kv.Key].Name, byId[kv.Key].PriceCents, kv.Value))
            .ToList();
        var total = lines.Sum(l => l.LineTotalCents);

        var payment = _payment.AcceptCash(total, request.CashPaidCents); // throws InsufficientPaymentException

        foreach (var (productId, quantity) in quantities)
            byId[productId].DecreaseStock(quantity);

        var order = Order.Create(
            lines, request.CashPaidCents, payment.ChangeCents,
            request.IdempotencyKey, _clock.GetUtcNow().UtcDateTime);

        await _orders.AddAsync(order, ct);
        await _uow.SaveChangesAsync(ct); // throws ConcurrencyConflictException on conflict

        foreach (var productId in quantities.Keys)
            await _notifier.NotifyStockChangedAsync(productId, byId[productId].StockQuantity, ct);

        return Map(order);
    }

    private static CheckoutResult Map(Order order)
    {
        var change = ChangeCalculator.Calculate(order.ChangeCents)
            .Select(p => new ChangePieceDto(p.DenominationCents, p.Count))
            .ToList();
        var lines = order.Lines
            .Select(l => new OrderLineDto(l.ProductId, l.ProductName, l.UnitPriceCents, l.Quantity, l.LineTotalCents))
            .ToList();
        return new CheckoutResult(
            order.Id, order.TotalCents, order.CashPaidCents, order.ChangeCents,
            change, lines, order.CreatedAtUtc);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Pos.Application.Tests --filter CheckoutServiceTests`
Expected: PASS — all checkout tests green.

- [ ] **Step 5: Commit**

```bash
git add src/backend/Pos.Application/Checkout/CheckoutService.cs tests/Pos.Application.Tests/CheckoutServiceTests.cs
git commit -m "feat(application): add checkout service with idempotency and concurrency retry"
```

---

## Task 9: CatalogService & ReportingService (TDD)

**Files:**
- Create: `src/backend/Pos.Application/Catalog/CatalogService.cs`
- Create: `src/backend/Pos.Application/Reporting/ReportingService.cs`
- Test: `tests/Pos.Application.Tests/CatalogServiceTests.cs`
- Test: `tests/Pos.Application.Tests/ReportingServiceTests.cs`

- [ ] **Step 1: Write the failing CatalogService tests**

Create `tests/Pos.Application.Tests/CatalogServiceTests.cs`:

```csharp
using NSubstitute;
using Pos.Application.Abstractions;
using Pos.Application.Catalog;
using Pos.Application.Exceptions;

namespace Pos.Application.Tests;

public class CatalogServiceTests
{
    private readonly IProductRepository _products = Substitute.For<IProductRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

    private CatalogService CreateSut() => new(_products, _uow);

    [Fact]
    public async Task GetProducts_LocalizesNamesWithFallback()
    {
        var brownie = TestData.Product(1, "Brownie");
        brownie.AddTranslation("et", "Brauni");
        var muffin = TestData.Product(2, "Muffin"); // no et translation
        _products.GetAllActiveAsync().Returns([brownie, muffin]);

        var et = await CreateSut().GetProductsAsync("et-EE");

        Assert.Equal("Brauni", et.Single(p => p.Id == 1).Name);
        Assert.Equal("Muffin", et.Single(p => p.Id == 2).Name); // falls back to canonical
    }

    [Fact]
    public async Task GetProducts_NullCulture_UsesCanonicalNames()
    {
        var brownie = TestData.Product(1, "Brownie");
        brownie.AddTranslation("et", "Brauni");
        _products.GetAllActiveAsync().Returns([brownie]);

        var result = await CreateSut().GetProductsAsync(null);

        Assert.Equal("Brownie", result.Single().Name);
    }

    [Fact]
    public async Task GetProduct_NotFound_ReturnsNull()
    {
        _products.GetByIdAsync(42).Returns((Pos.Domain.Catalog.Product?)null);
        Assert.Null(await CreateSut().GetProductAsync(42, "en"));
    }

    [Fact]
    public async Task SetStock_OnMissingProduct_Throws()
    {
        _products.GetByIdAsync(42).Returns((Pos.Domain.Catalog.Product?)null);
        await Assert.ThrowsAsync<ProductNotFoundException>(() => CreateSut().SetStockAsync(42, 5));
    }

    [Fact]
    public async Task SetStock_Negative_ThrowsInvalidQuantity()
    {
        _products.GetByIdAsync(1).Returns(TestData.Product(1));
        await Assert.ThrowsAsync<InvalidQuantityException>(() => CreateSut().SetStockAsync(1, -1));
    }

    [Fact]
    public async Task SetStock_Valid_UpdatesAndSaves()
    {
        var product = TestData.Product(1, stock: 0);
        _products.GetByIdAsync(1).Returns(product);

        await CreateSut().SetStockAsync(1, 25);

        Assert.Equal(25, product.StockQuantity);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test tests/Pos.Application.Tests --filter CatalogServiceTests`
Expected: FAIL — `CatalogService` does not exist.

- [ ] **Step 3: Implement CatalogService**

Create `src/backend/Pos.Application/Catalog/CatalogService.cs`:

```csharp
using Pos.Application.Abstractions;
using Pos.Application.Exceptions;
using Pos.Domain.Catalog;

namespace Pos.Application.Catalog;

public sealed class CatalogService
{
    private readonly IProductRepository _products;
    private readonly IUnitOfWork _uow;

    public CatalogService(IProductRepository products, IUnitOfWork uow)
    {
        _products = products;
        _uow = uow;
    }

    public async Task<IReadOnlyList<ProductDto>> GetProductsAsync(string? culture, CancellationToken ct = default)
    {
        var products = await _products.GetAllActiveAsync(ct);
        var neutral = ToNeutral(culture);
        return products.Select(p => ToDto(p, neutral)).ToList();
    }

    public async Task<ProductDto?> GetProductAsync(int id, string? culture, CancellationToken ct = default)
    {
        var product = await _products.GetByIdAsync(id, ct);
        return product is null ? null : ToDto(product, ToNeutral(culture));
    }

    public async Task SetStockAsync(int id, int quantity, CancellationToken ct = default)
    {
        if (quantity < 0)
            throw new InvalidQuantityException(id, quantity);

        var product = await _products.GetByIdAsync(id, ct)
            ?? throw new ProductNotFoundException(id);

        product.SetStock(quantity);
        await _uow.SaveChangesAsync(ct);
    }

    private static string? ToNeutral(string? culture)
        => string.IsNullOrWhiteSpace(culture) ? null : culture.Split('-')[0].ToLowerInvariant();

    private static ProductDto ToDto(Product p, string? neutralCulture)
        => new(p.Id, p.GetName(neutralCulture), p.Category.ToString(),
               p.PriceCents, p.StockQuantity, p.ImageKey, p.IsOutOfStock);
}
```

- [ ] **Step 4: Run to verify pass**

Run: `dotnet test tests/Pos.Application.Tests --filter CatalogServiceTests`
Expected: PASS.

- [ ] **Step 5: Write the failing ReportingService tests**

Create `tests/Pos.Application.Tests/ReportingServiceTests.cs`:

```csharp
using NSubstitute;
using Pos.Application.Abstractions;
using Pos.Application.Reporting;

namespace Pos.Application.Tests;

public class ReportingServiceTests
{
    private readonly IReportQueries _queries = Substitute.For<IReportQueries>();
    private readonly IProductRepository _products = Substitute.For<IProductRepository>();

    private ReportingService CreateSut() => new(_queries, _products);

    [Fact]
    public async Task GetSummary_JoinsLocalizedNamesOntoTotals()
    {
        _queries.GetTotalsAsync().Returns(new ReportTotals(
            TotalFundsCents: 500, OrderCount: 3,
            Items: [new ItemSold(ProductId: 1, QuantitySold: 4, RevenueCents: 260)]));

        var brownie = TestData.Product(1, "Brownie");
        brownie.AddTranslation("et", "Brauni");
        _products.GetAllActiveAsync().Returns([brownie]);

        var summary = await CreateSut().GetSummaryAsync("et");

        Assert.Equal(500, summary.TotalFundsCents);
        Assert.Equal(3, summary.OrderCount);
        var item = summary.Items.Single();
        Assert.Equal("Brauni", item.Name);
        Assert.Equal(4, item.QuantitySold);
        Assert.Equal(260, item.RevenueCents);
    }

    [Fact]
    public async Task GetSummary_UnknownProductId_FallsBackToPlaceholder()
    {
        _queries.GetTotalsAsync().Returns(new ReportTotals(
            100, 1, [new ItemSold(99, 1, 100)]));
        _products.GetAllActiveAsync().Returns([]);

        var summary = await CreateSut().GetSummaryAsync("en");

        Assert.Equal("#99", summary.Items.Single().Name);
    }
}
```

- [ ] **Step 6: Run to verify failure**

Run: `dotnet test tests/Pos.Application.Tests --filter ReportingServiceTests`
Expected: FAIL — `ReportingService` does not exist.

- [ ] **Step 7: Implement ReportingService**

Create `src/backend/Pos.Application/Reporting/ReportingService.cs`:

```csharp
using Pos.Application.Abstractions;

namespace Pos.Application.Reporting;

public sealed class ReportingService
{
    private readonly IReportQueries _queries;
    private readonly IProductRepository _products;

    public ReportingService(IReportQueries queries, IProductRepository products)
    {
        _queries = queries;
        _products = products;
    }

    public async Task<ReportSummaryDto> GetSummaryAsync(string? culture, CancellationToken ct = default)
    {
        var totals = await _queries.GetTotalsAsync(ct);
        var products = await _products.GetAllActiveAsync(ct);
        var byId = products.ToDictionary(p => p.Id);
        var neutral = string.IsNullOrWhiteSpace(culture) ? null : culture.Split('-')[0].ToLowerInvariant();

        var items = totals.Items
            .Select(i => new ItemSoldDto(
                i.ProductId,
                byId.TryGetValue(i.ProductId, out var p) ? p.GetName(neutral) : $"#{i.ProductId}",
                i.QuantitySold,
                i.RevenueCents))
            .ToList();

        return new ReportSummaryDto(totals.TotalFundsCents, totals.OrderCount, items);
    }
}
```

- [ ] **Step 8: Run to verify pass**

Run: `dotnet test tests/Pos.Application.Tests --filter ReportingServiceTests`
Expected: PASS.

- [ ] **Step 9: Commit**

```bash
git add src/backend/Pos.Application/Catalog/CatalogService.cs src/backend/Pos.Application/Reporting/ReportingService.cs tests/Pos.Application.Tests/CatalogServiceTests.cs tests/Pos.Application.Tests/ReportingServiceTests.cs
git commit -m "feat(application): add catalog and reporting services"
```

---

## Task 10: Full suite green & wrap-up

- [ ] **Step 1: Run the entire backend-core test suite**

Run: `dotnet test PosSystem.sln`
Expected: PASS — all domain and application tests green, 0 failures.

- [ ] **Step 2: Confirm a clean build with warnings as information**

Run: `dotnet build PosSystem.sln -warnaserror`
Expected: Build succeeded with 0 warnings. If nullable warnings appear in the EF private-ctor entities, confirm the `= null!;` initializers are present; fix any genuine warning rather than suppressing it.

- [ ] **Step 3: Commit any final fixes**

```bash
git add -A
git commit -m "chore: backend core suite green"
```

(If there is nothing to commit, skip this step.)

---

## Definition of Done (Plan 1)

- `dotnet test PosSystem.sln` is green.
- Domain has no dependency on Application or any infrastructure package.
- Application depends only on Domain and its own port interfaces — no EF, no ASP.NET, no SignalR.
- Change-making, stock rules, the payment invariant, idempotent replay, and concurrency retry are all covered by tests.

**Next:** Plan 2 (Backend Infrastructure + API) implements the ports (`IProductRepository`, `IOrderRepository`, `IUnitOfWork`, `IPaymentService`, `IStockNotifier`, `IReportQueries`) against EF Core + Postgres, adds the minimal API under **URL-segment versioning (`/api/v1/...`) via `Asp.Versioning.Http`** with a per-version OpenAPI document, Swagger (with a Bearer Authorize button), SignalR hub, JSON seeding, **JWT bearer auth protecting the admin/back-office surface** (`PUT /products/{id}/stock` and `GET /reports/summary` require the `staff` role) with a minimal `POST /api/v1/auth/token` issuing a JWT for a seeded staff credential, a **landing page (`GET /`) and `GET /health`**, ProblemDetails error mapping (mapping each exception type to the `errorCode`s in spec §5), and integration tests including the parallel-checkout concurrency test and the auth tests (401/403/200).
