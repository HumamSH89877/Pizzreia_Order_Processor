using FluentValidation.TestHelper;
using PizzeriaOrderProcessor.Domain;
using PizzeriaOrderProcessor.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using PizzeriaOrderProcessor.Validators;


namespace PizzeriaOrderProcessor.Tests
{
    public class RawOrderLineItemValidatorTests
    {
        private readonly IOptions<ValidationSettings> _validationSettings;
        private readonly IOptions<TimeSettings> _timeSettings;
        private readonly FakeTimeProvider _testClock;
        private readonly RawOrderLineItemValidator _validator;

        public RawOrderLineItemValidatorTests()
        {
            _testClock = new FakeTimeProvider(DateTimeOffset.UtcNow);

            _validationSettings = Microsoft.Extensions.Options.Options.Create(new ValidationSettings 
            {
                ItemQuantityUpperLimit = 99 
            });
            _timeSettings = Microsoft.Extensions.Options.Options.Create(new TimeSettings 
            {
                MaximumAdvanceOrderHours = 24 * 7
            });

            _validator = new RawOrderLineItemValidator(
                _validationSettings.Value, 
                _timeSettings.Value, 
                _testClock
                );
        }

        private RawOrderLineItem CreateValidRawItem(string orderId = "ORD-00001", string productId = "PRD-10001")
        {
            return new RawOrderLineItem
            {
                OrderId = orderId,
                ProductId = productId,
                Quantity = 1,
                DeliverAt = _testClock.GetUtcNow().AddHours(2).UtcDateTime,
                CreatedAt = _testClock.GetUtcNow().UtcDateTime,
                CustomerAddress = "123 Valid St"
            };
        }

        [Fact]
        public void Should_Have_Error_When_OrderId_Is_Null()
        {
            var model = CreateValidRawItem();
            model.OrderId = null!;
            var result = _validator.TestValidate(model);
            result.ShouldHaveValidationErrorFor(item => item.OrderId);
        }

        [Fact]
        public void Should_Have_Error_When_OrderId_Is_Empty()
        {
            var model = CreateValidRawItem();
            model.OrderId = string.Empty;
            var result = _validator.TestValidate(model);
            result.ShouldHaveValidationErrorFor(item => item.OrderId);
        }

        [Fact]
        public void Should_Have_Error_When_OrderId_Exceeds_MaxLength()
        {
            var model = CreateValidRawItem();
            model.OrderId = new string('A', 51); 
            var result = _validator.TestValidate(model);
            result.ShouldHaveValidationErrorFor(item => item.OrderId);
        }

        [Fact]
        public void Should_Not_Have_Error_When_OrderId_Is_Valid()
        {
            var model = CreateValidRawItem(orderId: "ORD-99999");
            var result = _validator.TestValidate(model);
            result.ShouldNotHaveValidationErrorFor(item => item.OrderId);
        }

        [Fact]
        public void Should_Have_Error_When_ProductId_Is_Null()
        {
            var model = CreateValidRawItem();
            model.ProductId = null!;
            var result = _validator.TestValidate(model);
            result.ShouldHaveValidationErrorFor(item => item.ProductId);
        }

        [Fact]
        public void Should_Have_Error_When_ProductId_Is_Empty()
        {
            var model = CreateValidRawItem();
            model.ProductId = string.Empty;
            var result = _validator.TestValidate(model);
            result.ShouldHaveValidationErrorFor(item => item.ProductId);
        }

        [Fact]
        public void Should_Have_Error_When_ProductId_Exceeds_MaxLength()
        {
            var model = CreateValidRawItem();
            model.ProductId = new string('A', 51); 
            var result = _validator.TestValidate(model);
            result.ShouldHaveValidationErrorFor(item => item.ProductId);
        }

        [Fact]
        public void Should_Not_Have_Error_When_ProductId_Is_Valid()
        {
            var model = CreateValidRawItem(productId: "PRD-99999");
            var result = _validator.TestValidate(model);
            result.ShouldNotHaveValidationErrorFor(item => item.ProductId);
        }

        [Fact]
        public void Should_Have_Error_When_Quantity_Is_Zero()
        {
            var model = CreateValidRawItem();
            model.Quantity = 0;
            var result = _validator.TestValidate(model);
            result.ShouldHaveValidationErrorFor(item => item.Quantity);
        }

        [Fact]
        public void Should_Have_Error_When_Quantity_Is_Negative()
        {
            var model = CreateValidRawItem();
            model.Quantity = -1;
            var result = _validator.TestValidate(model);
            result.ShouldHaveValidationErrorFor(item => item.Quantity);
        }

        [Fact]
        public void Should_Have_Error_When_Quantity_Exceeds_Limit()
        {
            var model = CreateValidRawItem();
            model.Quantity = _validationSettings.Value.ItemQuantityUpperLimit + 1;
            var result = _validator.TestValidate(model);
            result.ShouldHaveValidationErrorFor(item => item.Quantity);
        }

        [Fact]
        public void Should_Not_Have_Error_When_Quantity_Is_Valid()
        {
            var model = CreateValidRawItem();
            model.Quantity = _validationSettings.Value.ItemQuantityUpperLimit;
            var result = _validator.TestValidate(model);
            result.ShouldNotHaveValidationErrorFor(item => item.Quantity);
        }

        [Fact]
        public void Should_Have_Error_When_DeliverAt_Is_In_Past()
        {
            var item = CreateValidRawItem();
            var futureTime = _testClock.GetUtcNow().AddDays(1);
            _testClock.SetUtcNow(futureTime);
            item.CreatedAt = futureTime.UtcDateTime;
            item.DeliverAt = item.CreatedAt.AddMinutes(-1);

            var result = _validator.TestValidate(item);
            result.ShouldHaveValidationErrorFor(item => item.DeliverAt);
        }

        [Fact]
        public void Should_Have_Error_When_DeliverAt_Too_Far_In_Future()
        {
            var model = CreateValidRawItem();
            var futureTime = _testClock.GetUtcNow().AddHours(_timeSettings.Value.MaximumAdvanceOrderHours + 1); 
            model.DeliverAt = futureTime.UtcDateTime; 
            var result = _validator.TestValidate(model);
            result.ShouldHaveValidationErrorFor(item => item.DeliverAt);
        }

        [Fact]
        public void Should_Not_Have_Error_When_DeliverAt_Is_Valid()
        {
            var model = CreateValidRawItem();
            var futureTime = _testClock.GetUtcNow().AddHours(_timeSettings.Value.MaximumAdvanceOrderHours); 
            model.DeliverAt = futureTime.UtcDateTime; 
            var result = _validator.TestValidate(model);
            result.ShouldNotHaveValidationErrorFor(item => item.DeliverAt);
        }

        [Fact]
        public void Should_Have_Error_When_CustomerAddress_Is_Null()
        {
            var model = CreateValidRawItem();
            model.CustomerAddress = null!;
            var result = _validator.TestValidate(model);
            result.ShouldHaveValidationErrorFor(item => item.CustomerAddress);
        }

        [Fact]
        public void Should_Have_Error_When_CustomerAddress_Is_Empty()
        {
            var model = CreateValidRawItem();
            model.CustomerAddress = string.Empty;
            var result = _validator.TestValidate(model);
            result.ShouldHaveValidationErrorFor(item => item.CustomerAddress);
        }

        [Fact]
        public void Should_Not_Have_Error_When_CustomerAddress_Is_Valid()
        {
            var model = CreateValidRawItem();
            model.CustomerAddress = "Valid Address 123";
            var result = _validator.TestValidate(model);
            result.ShouldNotHaveValidationErrorFor(item => item.CustomerAddress);
        }
    }
}
