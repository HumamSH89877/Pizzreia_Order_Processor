using FluentValidation;
using Microsoft.Extensions.Options;
using PizzeriaOrderProcessor.Domain;
using PizzeriaOrderProcessor.Configuration;

namespace PizzeriaOrderProcessor.Validators
{
    public class OrderItemValidator : AbstractValidator<OrderItem>
    {
        public OrderItemValidator(IOptions<ValidationSettings> validationSettings)
        {
            ArgumentNullException.ThrowIfNull(validationSettings);
            
            var settings = validationSettings.Value;

            RuleFor(item => item.ProductId)
                .NotEmpty().WithMessage(ValidationConstants.ProductIdRequiredMessage)
                .Matches(ValidationConstants.ProductIdPattern).WithMessage(ValidationConstants.ProductIdFormatMessage);

            RuleFor(item => item.Quantity)
                .GreaterThan(0).WithMessage(ValidationConstants.QuantityPositiveMessage)
                .LessThanOrEqualTo(settings.ItemQuantityUpperLimit)
                .WithMessage(ValidationConstants.GetQuantityLimitMessage(settings.ItemQuantityUpperLimit));

            // This check ensures the product was successfully linked during reconstruction
            RuleFor(item => item.AssociatedProduct)
                 .NotNull().WithMessage("Product could not be found in catalog or was invalid.");
        }
    }
}