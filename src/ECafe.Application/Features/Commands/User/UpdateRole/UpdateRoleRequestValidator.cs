using FluentValidation;

namespace ECafe.Application.Features.Commands.User.UpdateRole
{
    public class UpdateRoleRequestValidator : AbstractValidator<UpdateRoleCommand>
    {
        public UpdateRoleRequestValidator()
        {
            RuleFor(x => x.UserId)
            .GreaterThan(0).WithMessage("UserId must be greater than 0.");

            RuleFor(x => x.RoleId)
                .GreaterThan(0).WithMessage("RoleId must be greater than 0.");

            RuleFor(x => x.RestaurantId)
                .GreaterThan(0).When(x => x.RestaurantId.HasValue);

        }
    }
}
