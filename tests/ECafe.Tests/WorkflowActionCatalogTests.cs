using System.Reflection;
using ECafe.Domain.Workflow;
using Xunit;

namespace ECafe.Tests;

public sealed class WorkflowActionCatalogTests
{
    [Theory]
    [InlineData(typeof(ContractActionCode), nameof(ContractActionCode.SendForSignature), "sendForSignature")]
    [InlineData(typeof(ContractActionCode), nameof(ContractActionCode.Approve), "approve")]
    [InlineData(typeof(ContractActionCode), nameof(ContractActionCode.Activate), "activate")]
    [InlineData(typeof(ContractActionCode), nameof(ContractActionCode.Terminate), "terminate")]
    [InlineData(typeof(ReservationActionCode), nameof(ReservationActionCode.SendPaymentInstruction), "sendPaymentInstruction")]
    [InlineData(typeof(ReservationActionCode), nameof(ReservationActionCode.SubmitPaymentProof), "submitPaymentProof")]
    [InlineData(typeof(ReservationActionCode), nameof(ReservationActionCode.ApprovePaymentProof), "approvePaymentProof")]
    [InlineData(typeof(ReservationActionCode), nameof(ReservationActionCode.RejectPaymentProof), "rejectPaymentProof")]
    [InlineData(typeof(ReservationActionCode), nameof(ReservationActionCode.CheckIn), "checkIn")]
    [InlineData(typeof(ReservationActionCode), nameof(ReservationActionCode.MarkArrived), "markArrived")]
    [InlineData(typeof(ReservationActionCode), nameof(ReservationActionCode.Cancel), "cancel")]
    [InlineData(typeof(ReservationActionCode), nameof(ReservationActionCode.Complete), "complete")]
    [InlineData(typeof(ReservationActionCode), nameof(ReservationActionCode.RequestRefund), "requestRefund")]
    [InlineData(typeof(ReservationActionCode), nameof(ReservationActionCode.WaiveDeposit), "waiveDeposit")]
    [InlineData(typeof(RefundActionCode), nameof(RefundActionCode.SubmitPayoutDetails), "submitPayoutDetails")]
    [InlineData(typeof(RefundActionCode), nameof(RefundActionCode.ViewPayoutDetails), "viewPayoutDetails")]
    [InlineData(typeof(RefundActionCode), nameof(RefundActionCode.SubmitTransfer), "submitTransfer")]
    [InlineData(typeof(RefundActionCode), nameof(RefundActionCode.ConfirmTransfer), "confirmTransfer")]
    [InlineData(typeof(RefundActionCode), nameof(RefundActionCode.DisputeTransfer), "disputeTransfer")]
    public void SplitCatalogsPreserveEveryStoredActionIdentifier(Type catalog, string fieldName, string expected)
    {
        var field = catalog.GetField(fieldName, BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
        Assert.NotNull(field);
        Assert.True(field.IsLiteral);
        Assert.Equal(typeof(string), field.FieldType);
        Assert.Equal(expected, field.GetRawConstantValue());
    }

    [Theory]
    [InlineData(typeof(ContractActionCode), 4)]
    [InlineData(typeof(ReservationActionCode), 10)]
    [InlineData(typeof(RefundActionCode), 5)]
    public void EachCatalogContainsOnlyItsOwnIdentifiersAndNoNestedCatalogs(Type catalog, int expectedCount)
    {
        Assert.True(catalog.IsAbstract && catalog.IsSealed);
        Assert.Empty(catalog.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic));
        Assert.Empty(catalog.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly));
        var fields = catalog.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
        Assert.Equal(expectedCount, fields.Length);
        Assert.All(fields, field => Assert.True(field.IsLiteral && field.FieldType == typeof(string)));
        Assert.Equal(fields.Length, fields.Select(field => field.GetRawConstantValue()).Distinct().Count());
    }
}
