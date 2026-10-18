using System.Reflection;
using ECafe.Api.Controllers;
using ECafe.Application.DTOs.Payment;
using ECafe.Domain.Entities;
using ECafe.Infrastructure.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ECafe.Tests;

public sealed class SessionBillContractTests
{
    [Fact]
    public void CheckoutAcceptsNoAmountOrSensitiveBodyAndRequiresAuthentication()
    {
        Assert.NotNull(typeof(SessionBillController).GetCustomAttribute<AuthorizeAttribute>());
        var method = typeof(SessionBillController).GetMethod(nameof(SessionBillController.Checkout))!;
        Assert.DoesNotContain(method.GetParameters(), parameter => parameter.GetCustomAttribute<FromBodyAttribute>() != null);
        var header = Assert.Single(method.GetParameters(), parameter => parameter.GetCustomAttribute<FromHeaderAttribute>() != null);
        Assert.Equal("Idempotency-Key", header.GetCustomAttribute<FromHeaderAttribute>()!.Name);
        Assert.Equal(new[] { "tableSessionId", "key", "cancellationToken" }, method.GetParameters().Select(parameter => parameter.Name));
        Assert.Equal(new[] { "Outcome" }, typeof(SimulatePaymentRequest).GetProperties().Select(property => property.Name));
    }

    [Fact]
    public void PostgreSqlModelEnforcesOnePendingPaymentAndSeparateSessionPurpose()
    {
        using var db = new ECafeDbContext(new DbContextOptionsBuilder<ECafeDbContext>()
            .UseNpgsql("Host=localhost;Database=translation_only").Options);
        var entity = db.Model.FindEntityType(typeof(Payment))!;
        var index = entity.GetIndexes().Single(candidate => candidate.GetDatabaseName() == "payments_pending_session_key");
        Assert.True(index.IsUnique);
        Assert.Equal("table_session_id IS NOT NULL AND payment_status_id = 3001 AND NOT \"IsDeleted\"", index.GetFilter());
        var reference = entity.GetForeignKeys().Single(key => key.Properties.Any(property => property.Name == nameof(Payment.TableSessionId)));
        Assert.Equal(DeleteBehavior.Restrict, reference.DeleteBehavior);
        Assert.DoesNotContain(nameof(Payment.ReservationId), reference.Properties.Select(property => property.Name));
        var sql = db.Payments.Where(payment => payment.TableSessionId == 1).ToQueryString();
        Assert.Contains("table_session_id", sql);
        Assert.Contains("bill_fingerprint", sql);
    }
}
