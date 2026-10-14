using ECafe.Application.DTOs.Order;
using ECafe.Domain.Exceptions;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace ECafe.Application.Services.Orders;

public sealed class CreateWaiterOrderValidator : AbstractValidator<CreateWaiterOrderRequest>
{
    public CreateWaiterOrderValidator(IOptions<OrderCreationOptions> options)
    {
        var limits = options.Value;
        RuleFor(request => request.Note).MaximumLength(limits.MaxNoteLength)
            .WithErrorCode(nameof(ErrorCode.InvalidOrderRequest));
        RuleFor(request => request.Items).NotEmpty()
            .WithErrorCode(nameof(ErrorCode.InvalidOrderRequest));
        RuleFor(request => request.Items).Must(items => items is null || items.Count <= limits.MaxLines)
            .WithErrorCode(nameof(ErrorCode.InvalidOrderRequest));
        RuleForEach(request => request.Items).NotNull()
            .WithErrorCode(nameof(ErrorCode.InvalidOrderRequest))
            .ChildRules(line =>
            {
                line.RuleFor(item => item.ItemId).GreaterThan(0).WithErrorCode(nameof(ErrorCode.InvalidOrderRequest));
                line.RuleFor(item => item.Quantity).InclusiveBetween(1, limits.MaxQuantityPerItem)
                    .WithErrorCode(nameof(ErrorCode.InvalidOrderRequest));
                line.RuleFor(item => item.Note).MaximumLength(limits.MaxLineNoteLength)
                    .WithErrorCode(nameof(ErrorCode.InvalidOrderRequest));
                line.RuleFor(item => item.ExpectedUnitPrice).Must(price => !price.HasValue || price.Value >= 0)
                    .WithErrorCode(nameof(ErrorCode.InvalidOrderRequest));
            });
    }
}
