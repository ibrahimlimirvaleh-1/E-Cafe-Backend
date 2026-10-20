using ECafe.Api.Filters;
using ECafe.Application.DTOs.Lookup;
using ECafe.Application.DTOs.Reservation;
using ECafe.Application.DTOs.Restaurant;
using ECafe.Application.DTOs.RestaurantContract;
using ECafe.Application.DTOs.Table;
using ECafe.Application.DTOs.Workflow;
using ECafe.Application.Services.Localization;
using ECafe.Application.Services.Localization.Concrete;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Policies;
using ECafe.Domain.Workflow;
using ECafe.Infrastructure.Context;
using ECafe.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ECafe.Tests;

public sealed class ContractLocalizationTests
{
    private static readonly TranslationBootstrap Bootstrap = new();

    [Theory]
    [InlineData("az", "detail")]
    [InlineData("en", "detail")]
    [InlineData("ru", "detail")]
    [InlineData("az", "list")]
    [InlineData("en", "list")]
    [InlineData("ru", "list")]
    [InlineData("az", "page")]
    [InlineData("en", "page")]
    [InlineData("ru", "page")]
    public async Task SeededContractStatusesAndNestedActionsLocalizeWithoutChangingBusinessFields(string language, string shape)
    {
        await using var db = Database();
        await db.Database.EnsureCreatedAsync();
        var states = await db.Statuses.Where(row => row.StatusTypeId == (int)ECafe.Domain.Enums.StatusType.Contract).OrderBy(row => row.Id).ToListAsync();
        var rules = await db.WorkflowActionRules.Where(row => row.FlowCode == WorkflowFlowCode.FromStatusType(ECafe.Domain.Enums.StatusType.Contract)).ToListAsync();
        Assert.Equal(Enum.GetValues<ContractStatus>().Length, states.Count);
        var originalStates = states.Select(row => row.Name).ToArray();
        var originalLabels = rules.Select(row => row.Label).ToArray();
        var expectedStates = ExpectedStatuses(language);
        var expectedActions = ExpectedActions(language);

        for (var index = 0; index < states.Count; index++)
        {
            var state = states[index];
            var stateRules = rules.Where(row => row.StatusId == state.Id).ToList();
            var response = new RestaurantContractResponse
            {
                Id = 53, RestaurantId = 1, StatusId = state.Id, StatusName = state.Name,
                ContractNumber = "Qaralama", SignedByUserName = "Aktiv", SignedByUserId = 11,
                Amount = 125m, PaymentPolicyId = 2, CommissionPercent = 5m,
                AvailableActions = stateRules.Select(MapAction).ToList()
            };
            var page = new PaginatedList<RestaurantContractResponse>([response], 40, 2, 20);
            object payload = shape switch { "detail" => response, "list" => new[] { response }, _ => page };
            await LocalizeAsync(payload, language);

            Assert.Equal(expectedStates[index], response.StatusName);
            Assert.Equal(state.Id, response.StatusId);
            Assert.Equal(53, response.Id);
            Assert.Equal(1, response.RestaurantId);
            Assert.Equal("Qaralama", response.ContractNumber);
            Assert.Equal("Aktiv", response.SignedByUserName);
            Assert.Equal(11, response.SignedByUserId);
            Assert.Equal(125m, response.Amount);
            Assert.Equal(2, response.PaymentPolicyId);
            Assert.Equal(5m, response.CommissionPercent);
            Assert.Equal(40, page.TotalCount);
            Assert.Equal(2, page.PageIndex);
            for (var actionIndex = 0; actionIndex < stateRules.Count; actionIndex++)
                AssertActionUnchangedExceptLabel(stateRules[actionIndex], response.AvailableActions[actionIndex], expectedActions);
        }
        Assert.Equal(originalStates, states.Select(row => row.Name));
        Assert.Equal(originalLabels, rules.Select(row => row.Label));
        Assert.False(db.ChangeTracker.HasChanges());
    }

    [Theory]
    [InlineData("az")]
    [InlineData("en")]
    [InlineData("ru")]
    public async Task StandaloneActionsAndStatusLookupsUseAllFourSeededLabelsAndKeepParserKeys(string language)
    {
        await using var db = Database();
        await db.Database.EnsureCreatedAsync();
        var rules = await db.WorkflowActionRules.Where(row => row.FlowCode == WorkflowFlowCode.FromStatusType(ECafe.Domain.Enums.StatusType.Contract)).OrderBy(row => row.Id).ToListAsync();
        var actions = rules.Select(MapAction).ToList();
        Assert.Equal(new[] { "activate", "approve", "sendForSignature", "terminate" }, actions.Select(row => row.Code).Distinct().Order());
        Assert.Equal(4, rules.Select(row => row.Label).Distinct().Count());
        await LocalizeAsync(actions.Select(row => row), language);
        for (var index = 0; index < rules.Count; index++)
            AssertActionUnchangedExceptLabel(rules[index], actions[index], ExpectedActions(language));

        var states = await db.Statuses.Where(row => row.StatusTypeId == (int)ECafe.Domain.Enums.StatusType.Contract).OrderBy(row => row.Id).ToListAsync();
        var lookups = states.Select(row => new LookupItemResponse { Id = row.Id, Code = "Contract:" + row.Id, Name = row.Name }).ToList();
        await LocalizeAsync(lookups, language);
        Assert.Equal(ExpectedStatuses(language), lookups.Select(row => row.Name));
        for (var index = 0; index < states.Count; index++)
        {
            Assert.Equal(states[index].Id, lookups[index].Id);
            Assert.Equal("Contract:" + states[index].Id, lookups[index].Code);
        }
        Assert.False(db.ChangeTracker.HasChanges());
    }

    [Theory]
    [InlineData("az")]
    [InlineData("en")]
    [InlineData("ru")]
    public async Task MatchingProperNamesAndRawStatusesArePreserved(string language)
    {
        var restaurant = new GetAllRestaurantsResponse { Name = "Aktiv" };
        var table = new TableResponse { Name = "Qaralama" };
        var reservation = new ReservationResponse { Status = "İmza gözləyir", RestaurantName = "Müqaviləni təsdiqlə" };
        var raw = new Status { Id = StatusIds.Contract(ContractStatus.Active), Name = "Aktiv" };
        var contract = new RestaurantContractResponse { StatusId = 9999, SignedByUserName = "Owner təsdiqlədi" };
        await LocalizeAsync(new object[] { restaurant, table, reservation, raw, contract }, language);
        Assert.Equal("Aktiv", restaurant.Name);
        Assert.Equal("Qaralama", table.Name);
        Assert.Equal("İmza gözləyir", reservation.Status);
        Assert.Equal("Müqaviləni təsdiqlə", reservation.RestaurantName);
        Assert.Equal("Aktiv", raw.Name);
        Assert.Null(contract.StatusName);
        Assert.Equal("Owner təsdiqlədi", contract.SignedByUserName);
        Assert.Equal(9999, contract.StatusId);
    }

    [Theory]
    [InlineData("az", "Redaktə edilmiş status", "Redaktə edilmiş əməliyyat")]
    [InlineData("en", "Edited status", "Edited action")]
    [InlineData("ru", "Изменённый статус", "Изменённое действие")]
    public async Task RequestPublishedOverridesDoNotChangeBuildSourceAliases(string language, string expectedStatus, string expectedAction)
    {
        const string statusKey = "api.literal.catalog.contractApproved";
        const string actionKey = "api.literal.catalog.contractSendForSignature";
        var entries = Bootstrap.Snapshot.Entries.ToDictionary(row => row.Key, row => row.Value);
        entries[statusKey] = new TranslationText("Redaktə edilmiş status", "Edited status", "Изменённый статус");
        entries[actionKey] = new TranslationText("Redaktə edilmiş əməliyyat", "Edited action", "Изменённое действие");
        var snapshot = new TranslationSnapshot("published-test", entries);
        var response = new RestaurantContractResponse
        {
            StatusId = StatusIds.Contract(ContractStatus.OwnerApproved), StatusName = "Owner təsdiqlədi",
            AvailableActions = [new() { Code = "sendForSignature", Label = "Sahibkar təsdiqinə göndər" }]
        };
        await LocalizeAsync(response, language, snapshot);
        Assert.Equal(expectedStatus, response.StatusName);
        Assert.Equal(expectedAction, response.AvailableActions[0].Label);
        Assert.Equal(statusKey, Bootstrap.SourceMatcher.Match("Owner təsdiqlədi")!.Key);
        Assert.Equal(actionKey, Bootstrap.SourceMatcher.Match("Sahibkar təsdiqinə göndər")!.Key);
        Assert.Equal("sendForSignature", response.AvailableActions[0].Code);
        Assert.Equal(StatusIds.Contract(ContractStatus.OwnerApproved), response.StatusId);
    }

    /// <summary>Yayımlanmış sorğu lüğəti və istehsal nəticə filtri ilə yalnız icazəli görünüş mətnlərini lokallaşdırır.</summary>
    private static async Task LocalizeAsync(object response, string language, TranslationSnapshot? snapshot = null)
    {
        var http = new DefaultHttpContext();
        http.Items[TextLocalizer.ContextKey] = (snapshot ?? Bootstrap.Snapshot).ForLanguage(language);
        var text = new TextLocalizer(new HttpContextAccessor { HttpContext = http }, Bootstrap);
        var context = new ActionContext(http, new RouteData(), new ActionDescriptor());
        var result = new OkObjectResult(response);
        await new LocalizedResponseFilter(text).OnResultExecutionAsync(new ResultExecutingContext(context, [], result, new object()),
            () => Task.FromResult(new ResultExecutedContext(context, [], result, new object())));
    }

    /// <summary>Seed qaydasından API görünüşü qurur; action kodunu və endpoint şablonunu mətn tərcüməsindən ayırır.</summary>
    private static WorkflowActionResponse MapAction(WorkflowActionRule rule) => new()
    {
        Code = rule.ActionCode, Label = rule.Label, Endpoint = rule.EndpointTemplate, HttpMethod = rule.HttpMethod,
        SortOrder = rule.SortOrder, RequiresConfirmation = rule.RequiresConfirmation, RequiresReason = rule.RequiresReason
    };

    /// <summary>Yalnız label-in dəyişdiyini, workflow icra müqaviləsinin isə eyni qaldığını yoxlayır.</summary>
    private static void AssertActionUnchangedExceptLabel(WorkflowActionRule rule, WorkflowActionResponse action, IReadOnlyDictionary<string, string> labels)
    {
        Assert.Equal(labels[rule.ActionCode], action.Label);
        Assert.Equal(rule.ActionCode, action.Code);
        Assert.Equal(rule.EndpointTemplate, action.Endpoint);
        Assert.Equal(rule.HttpMethod, action.HttpMethod);
        Assert.Equal(rule.SortOrder, action.SortOrder);
        Assert.Equal(rule.RequiresConfirmation, action.RequiresConfirmation);
        Assert.Equal(rule.RequiresReason, action.RequiresReason);
    }

    /// <summary>Bütün yeddi statusun ayrıca gözlənilən görünüşünü sabit enum ardıcıllığında verir.</summary>
    private static string[] ExpectedStatuses(string language) => language switch
    {
        "en" => ["Draft", "Awaiting signature", "Active", "Expired", "Cancelled", "Owner approved", "Scheduled"],
        "ru" => ["Черновик", "Ожидается подпись", "Действует", "Срок истёк", "Отменено", "Владелец подтвердил", "Запланировано"],
        _ => ["Qaralama", "İmza gözləyir", "Aktiv", "Müddəti bitib", "Ləğv edilib", "Sahibkar təsdiqlədi", "Planlaşdırılıb"]
    };

    /// <summary>Dörd contract əməliyyatı üçün kataloqdan asılı olmayan gözlənilən tərcümələri verir.</summary>
    private static IReadOnlyDictionary<string, string> ExpectedActions(string language)
    {
        string[] labels = language switch
        {
            "en" => ["Send for owner approval", "Terminate contract", "Approve contract", "Activate contract"],
            "ru" => ["Отправить на подтверждение владельцу", "Расторгнуть договор", "Подтвердить договор", "Активировать договор"],
            _ => ["Sahibkar təsdiqinə göndər", "Müqaviləni ləğv et", "Müqaviləni təsdiqlə", "Müqaviləni aktivləşdir"]
        };
        return new Dictionary<string, string> { ["sendForSignature"] = labels[0], ["terminate"] = labels[1], ["approve"] = labels[2], ["activate"] = labels[3] };
    }

    /// <summary>İstehsal seeder-lərini serverə toxunmadan real EF yaddaş bazasında işlədir.</summary>
    private static ECafeDbContext Database() => new(new DbContextOptionsBuilder<ECafeDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
