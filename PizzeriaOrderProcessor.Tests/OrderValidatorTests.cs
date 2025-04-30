using PizzeriaOrderProcessor.Domain;
using PizzeriaOrderProcessor.Configuration;
using FluentValidation;
using FluentValidation.TestHelper;
using FluentValidation.Results;
using Microsoft.Extensions.Options;
using Moq;
using Microsoft.Extensions.Logging.Abstractions;
using PizzeriaOrderProcessor.Application.Services.Interfaces;
using PizzeriaOrderProcessor.Validators;

namespace PizzeriaOrderProcessor.Tests
{
    public class OrderValidatorTests
    {
        private readonly IOptions<ValidationSettings> _validationSettings;
        private readonly IOptions<TimeSettings> _timeSettings;
        private readonly IOptions<OperatingHoursSettings> _operatingHoursSettings; 
        private readonly Mock<IClock> _mockClock; 
        private readonly Mock<IValidator<OrderItem>> _mockOrderItemValidator;
        private readonly OrderValidator _validator;
        private readonly DateTimeOffset _fixedTestTime = new DateTimeOffset(2024, 5, 15, 12, 0, 0, TimeSpan.Zero); // Consistent time for tests
        private readonly Product _mockProduct = new Product { ProductId = "PRD-00001", ProductName = "Test Product", UnitPriceExclVat = 10.0m, VAT = 0.05m };

        // Helper method to create a validator with a real OrderItemValidator
        private OrderValidator CreateValidatorWithRealItemValidator()
        {
            var realItemValidator = new OrderItemValidator(_validationSettings);
            
            return new OrderValidator(
                _validationSettings,
                _timeSettings,
                _operatingHoursSettings,
                _mockClock.Object,
                NullLogger<OrderValidator>.Instance,
                realItemValidator
            );
        }

        // Helper method to create a validator with custom time settings for performance testing
        private OrderValidator CreateValidatorForPerformanceTest()
        {
            // Custom time settings with minimal prep time
            var customTimeSettings = new TimeSettings
            {
                MaximumAdvanceOrderHours = 24 * 7,
                PrepBufferBaseMinutes = 0,
                PrepBufferPerItemMinutes = 0
            };
            
            // Custom operating hours (24/7)
            var customOperatingHours = new OperatingHoursSettings 
            { 
                Open = "00:00:00", 
                Close = "23:59:59" 
            };
            
            var customOptions = Options.Create(customTimeSettings);
            var customOperatingHoursOptions = Options.Create(customOperatingHours);
            
            return new OrderValidator(
                _validationSettings,
                customOptions,
                customOperatingHoursOptions,
                _mockClock.Object,
                NullLogger<OrderValidator>.Instance,
                _mockOrderItemValidator.Object
            );
        }

        private OrderValidator CreateValidatorWithSettings(ValidationSettings? validationSettings = null, TimeSettings? timeSettings = null, OperatingHoursSettings? operatingHoursSettings = null)
        {
            var mockClock = new Mock<IClock>();
            mockClock.Setup(c => c.UtcNow).Returns(_fixedTestTime);

            var mockOrderItemValidator = new Mock<IValidator<OrderItem>>();
            mockOrderItemValidator.Setup(v => v.Validate(It.IsAny<ValidationContext<OrderItem>>()))
                                .Returns(new ValidationResult());

            return new OrderValidator(
                validationSettings != null ? Options.Create(validationSettings) : _validationSettings,
                timeSettings != null ? Options.Create(timeSettings) : _timeSettings,
                operatingHoursSettings != null ? Options.Create(operatingHoursSettings) : _operatingHoursSettings,
                mockClock.Object,
                NullLogger<OrderValidator>.Instance,
                mockOrderItemValidator.Object
            );
        }

        public OrderValidatorTests()
        {
            _mockClock = new Mock<IClock>();
            _mockClock.Setup(c => c.UtcNow).Returns(_fixedTestTime);

            _validationSettings = Microsoft.Extensions.Options.Options.Create(new ValidationSettings 
            { 
                MinimumOrderAmount = 10m, 
                ItemQuantityUpperLimit = 100,
                AddressRegex = ".*" 
            });
            _timeSettings = Microsoft.Extensions.Options.Options.Create(new TimeSettings 
            {
                MaximumAdvanceOrderHours = 24 * 7,
                PrepBufferBaseMinutes = 30, 
                PrepBufferPerItemMinutes = 5 
            });
            _operatingHoursSettings = Microsoft.Extensions.Options.Options.Create(new OperatingHoursSettings { Open = "08:00:00", Close = "22:00:00" }); 

            _mockOrderItemValidator = new Mock<IValidator<OrderItem>>();
            // Default setup for item validator to return valid
            _mockOrderItemValidator.Setup(v => v.Validate(It.IsAny<ValidationContext<OrderItem>>()))
                                   .Returns(new FluentValidation.Results.ValidationResult()); 

            _validator = new OrderValidator(
                _validationSettings,
                _timeSettings,
                _operatingHoursSettings, 
                _mockClock.Object, 
                NullLogger<OrderValidator>.Instance, 
                _mockOrderItemValidator.Object
            );
        }

        private Order CreateValidOrderBase()
        {
            // Base order structure, specific tests will adjust dates/values
            return new Order
            {
                OrderId = "ORD-12345", 
                Items = new List<OrderItem> { CreateValidOrderItem() },
                CustomerAddress = "123 Test St",
                CreatedAt = _fixedTestTime.UtcDateTime,
                DeliverAt = _fixedTestTime.AddHours(2).UtcDateTime,
                TotalAmountIncludingVat = _validationSettings.Value.MinimumOrderAmount 
            };
        }

        private OrderItem CreateValidOrderItem(string productId = "PRD-00001", int quantity = 1)
        {
            return new OrderItem
            {
                ProductId = productId,
                Quantity = quantity,
                AssociatedProduct = productId == "PRD-00001" ? _mockProduct : null
            };
        }

        [Fact]
        public void Validate_ValidOrder_IsValid()
        {
            // Arrange
            var order = CreateValidOrderBase();
            _mockClock.Setup(c => c.UtcNow).Returns(_fixedTestTime);
            _mockOrderItemValidator.Setup(v => v.Validate(It.IsAny<ValidationContext<OrderItem>>()))
                                   .Returns(new FluentValidation.Results.ValidationResult()); 

            // Act
            var result = _validator.TestValidate(order);

            // Assert
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Validate_Items_NullOrEmpty_HasError()
        {
            var order = CreateValidOrderBase();
            
            // Test null Items collection
            order.Items = null!;
            var resultNull = _validator.TestValidate(order);
            resultNull.ShouldHaveValidationErrorFor(o => o.Items).WithErrorCode("NotEmptyValidator");
            
            // Test empty Items collection
            order.Items = new List<OrderItem>();
            var resultEmpty = _validator.TestValidate(order);
            resultEmpty.ShouldHaveValidationErrorFor(o => o.Items).WithErrorCode("NotEmptyValidator");
        }

        [Fact]
        public void Validate_DeliverAtOutsideOperatingHours_HasError()
        {
            // Arrange: Set clock to a time where DeliverAt (2 hours later) falls outside 8-22
            var timeOutsideHours = new DateTimeOffset(2024, 5, 15, 21, 0, 0, TimeSpan.Zero); // 9 PM
            _mockClock.Setup(c => c.UtcNow).Returns(timeOutsideHours);

            var order = CreateValidOrderBase();
            order.CreatedAt = timeOutsideHours.UtcDateTime;
            order.DeliverAt = timeOutsideHours.AddHours(2).UtcDateTime; // Deliver at 11 PM (23:00)

            // Act
            var result = _validator.TestValidate(order);

            // Assert
            result.ShouldHaveValidationErrorFor(o => o.DeliverAt).WithErrorCode("OutsideOperatingHours");
        }

        [Fact]
        public void Validate_DeliverAtInsideOperatingHours_IsValid()
        {
            // Arrange: Default setup uses _fixedTestTime (12:00 PM) and delivers 2 hours later (14:00), which is valid
            var order = CreateValidOrderBase();

            // Act
            var result = _validator.TestValidate(order);

            // Assert
            result.ShouldNotHaveValidationErrorFor(o => o.DeliverAt); // Specifically check DeliverAt is valid
            result.ShouldNotHaveAnyValidationErrors(); // Ensure overall validity too
        }

        [Fact]
        public void Validate_DeliverAtInPast_HasError()
        {
            // Arrange
            var order = CreateValidOrderBase();
            order.DeliverAt = _fixedTestTime.AddHours(-1).UtcDateTime; // Set delivery time 1 hour in the past

            // Act
            var result = _validator.TestValidate(order);

            // Assert
            result.ShouldHaveValidationErrorFor(o => o.DeliverAt).WithErrorCode("GreaterThanOrEqualValidator");
        }

        [Fact]
        public void Validate_DeliverAtWithInsufficientPrepTime_HasError()
        {
            // Arrange
            var order = CreateValidOrderBase();
            // Order has 1 item. Settings: Base=30min, PerItem=5min. Total required buffer = 35min.
            order.DeliverAt = _fixedTestTime.AddMinutes(30).UtcDateTime; // Deliver only 30 mins later (not enough)

            // Act
            var result = _validator.TestValidate(order);

            // Assert
            result.ShouldHaveValidationErrorFor(o => o.DeliverAt).WithErrorCode("InsufficientPrepTime");
        }

        [Fact]
        public void Validate_OrderTotalBelowMinimum_HasError()
        {
            var order = CreateValidOrderBase();
            order.TotalAmountIncludingVat = _validationSettings.Value.MinimumOrderAmount - 1;

            var result = _validator.TestValidate(order);
            result.ShouldHaveValidationErrorFor(o => o.TotalAmountIncludingVat);
        }

        [Fact]
        public void Validate_OrderTotalAtMinimum_IsValid()
        {
            var order = CreateValidOrderBase();
            order.TotalAmountIncludingVat = _validationSettings.Value.MinimumOrderAmount;

            var result = _validator.TestValidate(order);
            result.ShouldNotHaveValidationErrorFor(o => o.TotalAmountIncludingVat);
            result.ShouldNotHaveAnyValidationErrors();
        }

        // Customer Address Tests
        [Fact]
        public void Validate_CustomerAddress_NullOrEmpty_HasError()
        {
            var order = CreateValidOrderBase();
            
            // Test null address
            order.CustomerAddress = null!;
            var resultNull = _validator.TestValidate(order);
            resultNull.ShouldHaveValidationErrorFor(o => o.CustomerAddress).WithErrorCode("NotEmptyValidator");
            
            // Test empty address
            order.CustomerAddress = string.Empty;
            var resultEmpty = _validator.TestValidate(order);
            resultEmpty.ShouldHaveValidationErrorFor(o => o.CustomerAddress).WithErrorCode("NotEmptyValidator");
        }

        [Fact]
        public void Validate_InvalidAddressFormat_HasError()
        {
            var order = CreateValidOrderBase();
            order.CustomerAddress = "Invalid Address"; // Does not meet regex requirements
            
            // Override the default regex to make this test work correctly
            var customSettings = new ValidationSettings
            {
                MinimumOrderAmount = _validationSettings.Value.MinimumOrderAmount,
                ItemQuantityUpperLimit = _validationSettings.Value.ItemQuantityUpperLimit,
                AddressRegex = @"^\d+.*$" // Requires address to start with a number
            };
            
            var customValidator = CreateValidatorWithSettings(validationSettings: customSettings);
            var result = customValidator.TestValidate(order);
            result.ShouldHaveValidationErrorFor("CustomerAddress").WithErrorCode("PredicateValidator"); // Address uses Must() predicate
        }

        [Fact]
        public void Validate_AddressWithMinimumRequiredFields_IsValid()
        {
            var order = CreateValidOrderBase();
            order.CustomerAddress = "123 Main St, City, ST 12345";
            var result = _validator.TestValidate(order);
            result.ShouldNotHaveValidationErrorFor(o => o.CustomerAddress);
        }

        [Fact]
        public void Validate_AddressWithSpecialCharacters_IsValid()
        {
            var order = CreateValidOrderBase();
            order.CustomerAddress = "123 Main St., Apt #4B, City, ST 12345";
            var result = _validator.TestValidate(order);
            result.ShouldNotHaveValidationErrorFor(o => o.CustomerAddress);
        }

        // Product ID Tests
        [Fact]
        public void Validate_ProductId_NullOrEmpty_HasError()
        {
            var validator = CreateValidatorWithRealItemValidator();
            
            var order = CreateValidOrderBase();
            order.Items[0].ProductId = string.Empty;
            order.Items[0].AssociatedProduct = null; // Ensure this is null to match real validation
            var resultEmpty = validator.TestValidate(order);
            resultEmpty.ShouldHaveValidationErrorFor("Items[0].ProductId").WithErrorCode("NotEmptyValidator");
        }

        [Fact]
        public void Validate_ProductNotFound_HasError()
        {
            var validator = CreateValidatorWithRealItemValidator();
            
            var order = CreateValidOrderBase();
            order.Items[0].ProductId = "PRD-99999"; // Non-existent product ID
            order.Items[0].AssociatedProduct = null; // This is what causes the validation error
            
            var result = validator.TestValidate(order);
            result.ShouldHaveValidationErrorFor("Items[0].AssociatedProduct").WithErrorCode("NotNullValidator");
        }

        [Fact]
        public void Validate_ItemQuantityBelowMinimum_HasError()
        {
            var validator = CreateValidatorWithRealItemValidator();
            
            var order = CreateValidOrderBase();
            order.Items[0].Quantity = 0;
            
            var result = validator.TestValidate(order);
            result.ShouldHaveValidationErrorFor("Items[0].Quantity");
        }

        [Fact]
        public void Validate_ItemQuantityAboveMaximum_HasError()
        {
            var validator = CreateValidatorWithRealItemValidator();
            
            var order = CreateValidOrderBase();
            order.Items[0].Quantity = _validationSettings.Value.ItemQuantityUpperLimit + 1;
            
            var result = validator.TestValidate(order);
            result.ShouldHaveValidationErrorFor("Items[0].Quantity");
        }

        [Fact]
        public void Validate_ItemQuantityAtMaximum_IsValid()
        {
            var order = CreateValidOrderBase();
            order.Items[0].Quantity = _validationSettings.Value.ItemQuantityUpperLimit;
            var result = _validator.TestValidate(order);
            result.ShouldNotHaveValidationErrorFor(o => o.Items[0].Quantity);
        }

        // Multiple Items Tests
        [Fact]
        public void Validate_MultipleItemsWithSameDeliveryTime_IsValid()
        {
            var order = CreateValidOrderBase();
            order.Items.Add(CreateValidOrderItem(productId: "PRD-10002", quantity: 1));
            var result = _validator.TestValidate(order);
            result.ShouldNotHaveAnyValidationErrors();
        }

        // Order Total Tests
        [Fact]
        public void Validate_OrderTotalAboveMaximum_IsValid()
        {
            var order = CreateValidOrderBase();
            order.TotalAmountIncludingVat = decimal.MaxValue;
            var result = _validator.TestValidate(order);
            result.ShouldNotHaveValidationErrorFor(o => o.TotalAmountIncludingVat);
        }

        [Fact]
        public void Validate_OrderTotalNegative_HasError()
        {
            var order = CreateValidOrderBase();
            order.TotalAmountIncludingVat = -1;
            var result = _validator.TestValidate(order);
            result.ShouldHaveValidationErrorFor(o => o.TotalAmountIncludingVat);
        }

        // Delivery Time Edge Cases
        [Fact]
        public void Validate_DeliverAtExactlyMinimumPrepTime_IsValid()
        {
            var order = CreateValidOrderBase();
            var minPrepTime = _timeSettings.Value.PrepBufferBaseMinutes + _timeSettings.Value.PrepBufferPerItemMinutes;
            order.DeliverAt = _fixedTestTime.AddMinutes(minPrepTime).UtcDateTime;
            var result = _validator.TestValidate(order);
            result.ShouldNotHaveValidationErrorFor(o => o.DeliverAt);
        }

        [Fact]
        public void Validate_DeliverAtExactlyOperatingHoursStart_IsValid()
        {
            // ARRANGE
            var order = CreateValidOrderBase();
            // Set delivery time to exactly 08:00:00 (opening time) on the NEXT day
            var openingTime = new DateTime(_fixedTestTime.Year, _fixedTestTime.Month, _fixedTestTime.Day, 8, 0, 0, DateTimeKind.Utc).AddDays(1);
            order.DeliverAt = openingTime;
            
            // ACT
            var result = _validator.TestValidate(order);
            
            // ASSERT
            result.ShouldNotHaveValidationErrorFor(o => o.DeliverAt);
        }

        [Fact]
        public void Validate_DeliverAtExactlyOperatingHoursEnd_IsValid()
        {
            var order = CreateValidOrderBase();
            order.DeliverAt = new DateTime(2024, 5, 15, 22, 0, 0, DateTimeKind.Utc);
            var result = _validator.TestValidate(order);
            result.ShouldNotHaveValidationErrorFor(o => o.DeliverAt);
        }

        // Edge Cases for IDs
        [Fact]
        public void Validate_OrderIdWithMaximumAllowedNumber_IsValid()
        {
            var order = CreateValidOrderBase();
            order.OrderId = "ORD-99999";
            var result = _validator.TestValidate(order);
            result.ShouldNotHaveValidationErrorFor(o => o.OrderId);
        }

        [Fact]
        public void Validate_ProductIdWithMaximumAllowedNumber_IsValid()
        {
            var order = CreateValidOrderBase();
            order.Items[0].ProductId = "PRD-99999";
            var result = _validator.TestValidate(order);
            result.ShouldNotHaveValidationErrorFor(o => o.Items[0].ProductId);
        }

        // Special Character Tests
        [Fact]
        public void Validate_OrderIdWithSpecialCharacters_HasError()
        {
            var order = CreateValidOrderBase();
            order.OrderId = "ORD-12345!";
            var result = _validator.TestValidate(order);
            result.ShouldHaveValidationErrorFor(o => o.OrderId);
        }

        
        // Date/Time Format Tests
        [Fact]
        public void Validate_InvalidDeliverAtFormat_HasError()
        {
            var order = CreateValidOrderBase();
            order.DeliverAt = new DateTime(2024, 5, 15, 14, 0, 0, DateTimeKind.Local);
            var result = _validator.TestValidate(order);
            result.ShouldHaveValidationErrorFor(o => o.DeliverAt);
        }

        [Fact]
        public void Validate_DifferentTimeZones_HasError()
        {
            var order = CreateValidOrderBase();
            order.CreatedAt = new DateTime(2024, 5, 15, 12, 0, 0, DateTimeKind.Utc);
            order.DeliverAt = new DateTime(2024, 5, 15, 14, 0, 0, DateTimeKind.Local); // Different timezone
            var result = _validator.TestValidate(order);
            result.ShouldHaveValidationErrorFor(o => o.DeliverAt);
        }

        [Fact]
        public void Validate_SameTimeZones_IsValid()
        {
            var order = CreateValidOrderBase();
            var utcTime = new DateTime(2024, 5, 15, 12, 0, 0, DateTimeKind.Utc);
            order.CreatedAt = utcTime;
            order.DeliverAt = utcTime.AddHours(2);
            var result = _validator.TestValidate(order);
            result.ShouldNotHaveValidationErrorFor(o => o.DeliverAt);
        }

        [Fact]
        public void Validate_DeliveryTimeExactlyOneDayApart_IsValid()
        {
            var order = CreateValidOrderBase();
            order.DeliverAt = order.CreatedAt.AddDays(1);
            var result = _validator.TestValidate(order);
            result.ShouldNotHaveValidationErrorFor(o => o.DeliverAt);
        }

        [Fact]
        public void Validate_DeliveryTimeExactlyOneWeekApart_IsValid()
        {
            var order = CreateValidOrderBase();
            order.DeliverAt = order.CreatedAt.AddDays(7);
            var result = _validator.TestValidate(order);
            result.ShouldNotHaveValidationErrorFor(o => o.DeliverAt);
        }

        // Composite Tests
        [Fact]
        public void Validate_MultipleErrorsInSingleOrder()
        {
            var validator = CreateValidatorWithRealItemValidator();
            
            // ARRANGE
            var order = CreateValidOrderBase();
            
            // Set multiple validation errors
            order.OrderId = "INVALID-ID"; // Invalid format
            order.CustomerAddress = ""; // Empty address
            order.DeliverAt = _fixedTestTime.AddDays(-1).UtcDateTime; // Past delivery time
            order.TotalAmountIncludingVat = 5.00m; // Below minimum
            
            // Add an item with invalid ProductId
            order.Items.Add(new OrderItem { ProductId = "", Quantity = 1, AssociatedProduct = null });
            
            // ACT
            var result = validator.TestValidate(order);
            
            // ASSERT
            // Check that all expected errors are present
            result.ShouldHaveValidationErrorFor(o => o.OrderId);
            result.ShouldHaveValidationErrorFor(o => o.CustomerAddress);
            result.ShouldHaveValidationErrorFor(o => o.DeliverAt);
            result.ShouldHaveValidationErrorFor(o => o.TotalAmountIncludingVat);
            result.ShouldHaveValidationErrorFor("Items[1].ProductId");
        }

        // Configuration Edge Cases
        [Fact]
        public void Validate_WithMinimumOrderAmountZero_IsValid()
        {
            var order = CreateValidOrderBase();
            order.TotalAmountIncludingVat = 0;
            var settings = new ValidationSettings { MinimumOrderAmount = 0 };
            var validator = CreateValidatorWithSettings(validationSettings: settings);
            var result = validator.TestValidate(order);
            result.ShouldNotHaveValidationErrorFor(o => o.TotalAmountIncludingVat);
        }

        [Fact]
        public void Validate_WithMaximumAdvanceOrderHoursZero_HasError()
        {
            var order = CreateValidOrderBase();
            var settings = new TimeSettings { MaximumAdvanceOrderHours = 0 };
            var validator = CreateValidatorWithSettings(timeSettings: settings);
            var result = validator.TestValidate(order);
            result.ShouldHaveValidationErrorFor(o => o.DeliverAt);
        }

        // Performance Test
        [Fact]
        public void Validate_LargeNumberOfItems_Performance()
        {
            var validator = CreateValidatorForPerformanceTest();
            
            var order = CreateValidOrderBase();
            // Ensure DeliverAt is set to a valid time that won't fail validation
            order.DeliverAt = _fixedTestTime.AddHours(12).UtcDateTime; // 12 hours in the future (within operating hours)
            
            // Add 1000 items
            for (int i = 1; i < 1000; i++)
            {
                order.Items.Add(CreateValidOrderItem(productId: $"PRD-1000{i}", quantity: 1));
            }

            // Ensure the mock validator returns success for all items
            _mockOrderItemValidator.Setup(v => v.Validate(It.IsAny<ValidationContext<OrderItem>>()))
                                  .Returns(new ValidationResult());
            
            var stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();
            var result = validator.TestValidate(order);
            stopwatch.Stop();

            result.ShouldNotHaveAnyValidationErrors();
            Assert.True(stopwatch.ElapsedMilliseconds < 10000, $"Validation took too long ({stopwatch.ElapsedMilliseconds}ms)"); // Increased timeout
        }

        [Fact]
        public void Validate_OrderWithInvalidRequiredFields_IsRejected()
        {
            // ARRANGE
            var order = CreateValidOrderBase();
            order.OrderId = string.Empty; // Empty OrderId
            order.CustomerAddress = string.Empty; // Empty address
            order.DeliverAt = _fixedTestTime.AddHours(-1).UtcDateTime; // Past delivery time
            
            // ACT
            var result = _validator.TestValidate(order);
            
            // ASSERT
            result.ShouldHaveValidationErrorFor(o => o.OrderId);
            result.ShouldHaveValidationErrorFor(o => o.CustomerAddress);
            result.ShouldHaveValidationErrorFor(o => o.DeliverAt);
        }

        [Fact]
        public void Validate_DeliverAtTooFarInFuture_HasError()
        {
            var order = CreateValidOrderBase();
            // MaxAdvanceOrderHours = 168 (7 days)
            order.DeliverAt = _fixedTestTime.AddHours(169).UtcDateTime;

            var result = _validator.TestValidate(order);
            result.ShouldHaveValidationErrorFor(o => o.DeliverAt).WithErrorCode("TooFarInAdvance");
        }

        // Order ID Tests
        [Fact]
        public void Validate_OrderId_NullOrEmpty_HasError()
        {
            var order = CreateValidOrderBase();
            
            // Test null OrderId
            order.OrderId = null!;
            var resultNull = _validator.TestValidate(order);
            resultNull.ShouldHaveValidationErrorFor(o => o.OrderId).WithErrorCode("NotEmptyValidator");
            
            // Test empty OrderId
            order.OrderId = string.Empty;
            var resultEmpty = _validator.TestValidate(order);
            resultEmpty.ShouldHaveValidationErrorFor(o => o.OrderId).WithErrorCode("NotEmptyValidator");
        }

        [Fact]
        public void Validate_InvalidOrderIdFormat_HasError()
        {
            var order = CreateValidOrderBase();
            order.OrderId = "INVALID-ORDER-ID!";
            var result = _validator.TestValidate(order);
            result.ShouldHaveValidationErrorFor(o => o.OrderId).WithErrorCode("RegularExpressionValidator");
        }
    }
}
