using ECafe.Application.Common.Outbox;
using ECafe.Application.Services;
using ECafe.Application.Services.Monitoring.Abstract;
using ECafe.Application.Services.Outbox.Concrete;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace ECafe.Tests;

public sealed class RetiredSmsOutboxTests
{
    [Fact]
    public async Task LegacySmsEventIsReadOnlyAndNeverProcessed()
    {
        await using var context = new ECafeDbContext(new DbContextOptionsBuilder<ECafeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var legacySms = new OutboxEvent
        {
            Id = Guid.NewGuid(), EventType = OutboxEventTypes.SmsNotificationRequested,
            AggregateType = "Contract", AggregateId = 1,
            Payload = "{}", OccurredAt = DateTime.UtcNow
        };
        context.OutboxEvents.Add(legacySms);
        await context.SaveChangesAsync();

        var repository = new BaseRepository<OutboxEvent>(context);
        var configuration = new ConfigurationBuilder().Build();
        var processor = new EmailOutboxManager(repository, new Mock<IEmailService>().Object,
            new Mock<ICriticalEventReporter>().Object, configuration);
        var admin = new OutboxAdminManager(repository, configuration);

        Assert.Equal(0, await processor.ProcessPendingAsync(10));
        Assert.Null(legacySms.ProcessedAt);
        var history = await admin.GetMessageAsync(legacySms.Id);
        Assert.Equal((int)OutboxMessageStatus.Failed, history.StatusId);
        await Assert.ThrowsAsync<BusinessRuleException>(() => admin.RetryAsync(legacySms.Id));
    }
}
