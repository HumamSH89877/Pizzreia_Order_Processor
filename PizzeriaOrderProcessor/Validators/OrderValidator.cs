using System.Globalization;
using System.Text.RegularExpressions;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PizzeriaOrderProcessor.Domain;
using PizzeriaOrderProcessor.Configuration;
using PizzeriaOrderProcessor.Application.Services.Interfaces;

namespace PizzeriaOrderProcessor.Validators
{
    public class OrderValidator : AbstractValidator<Order>
    {
        private readonly ValidationSettings _validationSettings;
        private readonly TimeSettings _timeSettings;
        private readonly IClock _clock;
        private readonly ILogger<OrderValidator> _logger;

        private readonly TimeOnly _parsedOpenTime;
        private readonly TimeOnly _parsedCloseTime;
        private readonly bool _operatingHoursValid; 

        public OrderValidator(
            IOptions<ValidationSettings> validationSettings,
            IOptions<TimeSettings> timeSettings,
            IOptions<OperatingHoursSettings> operatingHoursOptions,
            IClock clock,
            ILogger<OrderValidator> logger,
            IValidator<OrderItem> orderItemValidator) 
        {
            ArgumentNullException.ThrowIfNull(validationSettings);
            ArgumentNullException.ThrowIfNull(timeSettings);
            ArgumentNullException.ThrowIfNull(operatingHoursOptions);
            ArgumentNullException.ThrowIfNull(clock);
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(orderItemValidator);
            
            _validationSettings = validationSettings.Value;
            _timeSettings = timeSettings.Value;
            _clock = clock;
            _logger = logger;

            _operatingHoursValid = TryParseOperatingHours(operatingHoursOptions.Value, out _parsedOpenTime, out _parsedCloseTime);

            // --- Validation Rules ---
            RuleFor(order => order.OrderId)
                .NotEmpty().WithMessage(ValidationConstants.OrderIdRequiredMessage)
                .Matches(ValidationConstants.OrderIdPattern).WithMessage(ValidationConstants.OrderIdFormatMessage);

            RuleFor(order => order.Items)
                .NotEmpty().WithMessage(ValidationConstants.OrderItemsRequiredMessage);

            RuleForEach(order => order.Items).SetValidator(orderItemValidator);

            RuleFor(order => order.CustomerAddress)
                .NotEmpty().WithMessage(ValidationConstants.CustomerAddressRequiredMessage)
                .Must(BeValidAddressFormat).WithMessage("Customer address format is invalid.");

            RuleFor(order => order.DeliverAt)
                .NotEmpty().WithMessage(ValidationConstants.DeliveryTimeRequiredMessage)
                .GreaterThanOrEqualTo(order => order.CreatedAt)
                    .WithMessage(order => $"Delivery time ({order.DeliverAt:O}) must be after or the same as creation time ({order.CreatedAt:O}).")
                .Must(BeWithinOperatingHours).When(_ => _operatingHoursValid).WithErrorCode("OutsideOperatingHours")
                    .WithMessage(order => $"Delivery time ({TimeOnly.FromDateTime(order.DeliverAt):HH:mm:ss}) is outside operating hours ({_parsedOpenTime:HH:mm:ss} - {_parsedCloseTime:HH:mm:ss}).")
                .Must(BeAfterMinimumPrepTime).WithErrorCode("InsufficientPrepTime").WithMessage(CalculatePrepTimeMessage) // Dynamic message
                .Must(BeWithinMaximumAdvanceTime).WithErrorCode("TooFarInAdvance").WithMessage(CalculateMaxAdvanceMessage); // Dynamic message

            // Minimum Order Amount validation
            RuleFor(order => order.TotalAmountIncludingVat)
                 .GreaterThanOrEqualTo(_validationSettings.MinimumOrderAmount)
                 .WithMessage(order => $"Order total ({order.TotalAmountIncludingVat:N2} AED) is below the minimum required amount ({_validationSettings.MinimumOrderAmount:N2} AED).");

            // Rule to ensure operating hours config was valid before applying rules
            RuleFor(order => order) 
                .Must(_ => _operatingHoursValid)
                .WithMessage("Configuration for OperatingHours (Open/Close times) is invalid. Cannot validate delivery time.")
                .WithName("OperatingHoursConfiguration"); // Give it a specific name for clarity
        }

        private bool TryParseOperatingHours(OperatingHoursSettings settings, out TimeOnly openTime, out TimeOnly closeTime)
        {
            openTime = default;
            closeTime = default;
            const string timeFormat = "HH:mm:ss"; 

            if (string.IsNullOrWhiteSpace(settings.Open) || string.IsNullOrWhiteSpace(settings.Close))
            {
                 _logger.LogError("OperatingHours Open ('{Open}') or Close ('{Close}') time is null or empty in configuration.", settings.Open, settings.Close);
                 return false;
            }

            bool openParsed = TimeOnly.TryParseExact(settings.Open, timeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out openTime);
            if (!openParsed)
            {
                 _logger.LogError("Failed to parse OperatingHours:Open value '{OpenTime}' using '{Format}' format.", settings.Open, timeFormat);
            }

            bool closeParsed = TimeOnly.TryParseExact(settings.Close, timeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out closeTime);
             if (!closeParsed)
            {
                 _logger.LogError("Failed to parse OperatingHours:Close value '{CloseTime}' using '{Format}' format.", settings.Close, timeFormat);
            }

            return openParsed && closeParsed;
        }


        private bool BeValidAddressFormat(string address)
        {
            if (string.IsNullOrWhiteSpace(address)) return false; 
         
            try
            {
                return Regex.IsMatch(address, _validationSettings.AddressRegex, RegexOptions.None, TimeSpan.FromMilliseconds(150));
            }
            catch (RegexMatchTimeoutException rex)
            {
                 _logger.LogWarning(rex, "Regex timeout during address validation for address '{Address}' using pattern: {Regex}", address, _validationSettings.AddressRegex);
                 return false; // Treat timeout as invalid match
            }
            catch (ArgumentException argEx) 
            {
                 _logger.LogError(argEx, "Invalid regex pattern configured for address validation: {Regex}", _validationSettings.AddressRegex);
                 return false; // Treat pattern error as invalid match
            }
        }

        // Use the pre-parsed fields _parsedOpenTime and _parsedCloseTime
        private bool BeWithinOperatingHours(DateTime deliverAt)
        {
            var deliveryTime = TimeOnly.FromDateTime(deliverAt);
            // Handle overnight case (e.g., Open 22:00, Close 06:00)
            if (_parsedOpenTime < _parsedCloseTime) // Standard day case (e.g., 08:00 - 22:00)
            {
                return deliveryTime >= _parsedOpenTime && deliveryTime <= _parsedCloseTime;
            }
            else // Overnight case (e.g., 21:00 - 05:00)
            {
                return deliveryTime >= _parsedOpenTime || deliveryTime <= _parsedCloseTime;
            }
        }

        private bool BeAfterMinimumPrepTime(Order order, DateTime deliverAt)
        {
            var now = _clock.UtcNow;
            int itemCount = order.Items?.Count ?? 0;
            var totalPrepMinutes = _timeSettings.PrepBufferBaseMinutes + itemCount * _timeSettings.PrepBufferPerItemMinutes;
            var earliestDeliveryTime = now.AddMinutes(totalPrepMinutes);
            return deliverAt >= earliestDeliveryTime;
        }

         // Helper for dynamic message generation
         private string CalculatePrepTimeMessage(Order order)
         {
             var now = _clock.UtcNow;
             int itemCount = order.Items?.Count ?? 0;
             var totalPrepMinutes = _timeSettings.PrepBufferBaseMinutes + itemCount * _timeSettings.PrepBufferPerItemMinutes;
             var earliestDeliveryTime = now.AddMinutes(totalPrepMinutes);
             return $"Delivery time ({order.DeliverAt:o}) must be at least {totalPrepMinutes} minutes from current time ({now:o}). Earliest allowed: {earliestDeliveryTime:o}.";
         }


        private bool BeWithinMaximumAdvanceTime(DateTime deliverAt)
        {
            var now = _clock.UtcNow;
            var latestDeliveryTime = now.AddHours(_timeSettings.MaximumAdvanceOrderHours);
            return deliverAt <= latestDeliveryTime;
        }

         // Helper for dynamic message generation
        private string CalculateMaxAdvanceMessage(Order order) // needed for context signature
        {
             var now = _clock.UtcNow;
             var latestDeliveryTime = now.AddHours(_timeSettings.MaximumAdvanceOrderHours);
             return $"Delivery time ({order.DeliverAt:o}) cannot be more than {_timeSettings.MaximumAdvanceOrderHours} hours from current time ({now:o}). Latest allowed: {latestDeliveryTime:o}.";
        }
    }
}
