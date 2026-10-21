using System.Reflection;
using ECafe.Application.Mappings;
using ECafe.Application.Services.Orders.Abstract;
using ECafe.Application.Services.Orders.Concrete;
using ECafe.Domain.Entities;
using ECafe.Domain.Policies;
using Xunit;

namespace ECafe.Tests;

public sealed class OrderModuleStructureTests
{
    [Theory]
    [InlineData(typeof(OrderManager), typeof(IOrderService))]
    [InlineData(typeof(KitchenOrderManager), typeof(IKitchenOrderService))]
    [InlineData(typeof(CustomerOrderTrackingManager), typeof(ICustomerOrderTrackingService))]
    [InlineData(typeof(OrderStockManager), typeof(IOrderStockService))]
    public void ServicesUseSeparateContractsAndImplementations(Type implementation, Type contract)
    {
        Assert.Equal("ECafe.Application.Services.Orders.Abstract", contract.Namespace);
        Assert.Equal("ECafe.Application.Services.Orders.Concrete", implementation.Namespace);
        Assert.True(contract.IsInterface);
        Assert.True(contract.IsAssignableFrom(implementation));
        var methods = implementation.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
        Assert.DoesNotContain(methods, method => method.Name.StartsWith("Map", StringComparison.Ordinal) ||
            method.Name.StartsWith("Project", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(typeof(OrderServingPolicy))]
    [InlineData(typeof(KitchenOrderProgressPolicy))]
    [InlineData(typeof(OrderStockPolicy))]
    [InlineData(typeof(InventoryStockPolicy))]
    public void BusinessPoliciesBelongToIndependentDomainLayer(Type policy)
    {
        Assert.Equal("ECafe.Domain.Policies", policy.Namespace);
        Assert.Same(typeof(Order).Assembly, policy.Assembly);
        Assert.DoesNotContain(policy.Assembly.GetReferencedAssemblies(), reference =>
            reference.Name?.StartsWith("ECafe.Application", StringComparison.Ordinal) == true);
    }

    [Theory]
    [InlineData(typeof(OrderResponseMapper))]
    [InlineData(typeof(KitchenOrderResponseMapper))]
    [InlineData(typeof(CustomerOrderTrackingMapper))]
    [InlineData(typeof(OrderStockMovementMapper))]
    public void ResponseMappingBelongsToMappingLayer(Type mapper)
    {
        Assert.Equal("ECafe.Application.Mappings", mapper.Namespace);
        Assert.Same(typeof(IOrderService).Assembly, mapper.Assembly);
    }
}
