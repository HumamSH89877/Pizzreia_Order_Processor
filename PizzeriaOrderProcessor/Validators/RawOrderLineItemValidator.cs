using FluentValidation;
using PizzeriaOrderProcessor.Configuration;
using PizzeriaOrderProcessor.Domain;

namespace PizzeriaOrderProcessor.Validators
{
    public class RawOrderLineItemValidator : AbstractValidator<RawOrderLineItem>
    {
        public RawOrderLineItemValidator(ValidationSettings validationSettings, TimeSettings timeSettings, TimeProvider timeProvider)
        {
            ArgumentNullException.ThrowIfNull(validationSettings);
            ArgumentNullException.ThrowIfNull(timeSettings);
            ArgumentNullException.ThrowIfNull(timeProvider);
            
            // Compute max future as DateTime to match RawOrderLineItem.CreatedAt type
            var maxFuture = timeProvider.GetUtcNow().UtcDateTime.AddHours(timeSettings.MaximumAdvanceOrderHours);

            RuleFor(x => x.OrderId)
                .NotEmpty().WithMessage(ValidationConstants.OrderIdRequiredMessage)
                .Matches(ValidationConstants.OrderIdPattern).WithMessage(ValidationConstants.OrderIdFormatMessage);

            RuleFor(x => x.ProductId)
                .NotEmpty().WithMessage(ValidationConstants.ProductIdRequiredMessage)
                .Matches(ValidationConstants.ProductIdPattern).WithMessage(ValidationConstants.ProductIdFormatMessage);

            RuleFor(x => x.Quantity)
                .GreaterThan(0).WithMessage(ValidationConstants.QuantityPositiveMessage)
                .LessThanOrEqualTo(validationSettings.ItemQuantityUpperLimit)
                .WithMessage(ValidationConstants.GetQuantityLimitMessage(validationSettings.ItemQuantityUpperLimit));

            RuleFor(x => x.CreatedAt)
                .Must(createdAt => createdAt <= maxFuture)
                .WithMessage($"CreatedAt must be no more than {timeSettings.MaximumAdvanceOrderHours} hours in the future");

            RuleFor(x => x.DeliverAt)
                .GreaterThanOrEqualTo(x => x.CreatedAt)
                .WithMessage("DeliverAt must be on or after CreatedAt")
                .Must(deliverAt => deliverAt <= maxFuture)
                .WithMessage($"DeliverAt must be no more than {timeSettings.MaximumAdvanceOrderHours} hours in the future");

            RuleFor(x => x.CustomerAddress)
                .NotEmpty().WithMessage(ValidationConstants.AddressRequiredMessage);
        }
    }
}
