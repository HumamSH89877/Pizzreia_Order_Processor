namespace PizzeriaOrderProcessor.Validators
{
    /// <summary>
    /// Contains common validation patterns and constants to improve DRY compliance
    /// </summary>
    public static class ValidationConstants
    {
        // Common regex patterns
        public const string OrderIdPattern = @"^ORD-\d{5}$";
        public const string ProductIdPattern = @"^PRD-\d{5}$";
        
        // Common validation messages
        public const string OrderIdFormatMessage = "OrderId must match format ORD-NNNNN";
        public const string ProductIdFormatMessage = "ProductId must match format PRD-NNNNN";
        public const string ProductIdRequiredMessage = "Product ID is required.";
        public const string OrderIdRequiredMessage = "Order ID is required.";
        public const string QuantityPositiveMessage = "Quantity must be greater than zero";
        public const string AddressRequiredMessage = "CustomerAddress must not be empty";
        public const string CustomerAddressRequiredMessage = "Customer address is required.";
        public const string DeliveryTimeRequiredMessage = "Delivery time is required.";
        public const string OrderItemsRequiredMessage = "Order must contain at least one valid item.";
        
        // Format helper to keep quantity limit messages consistent
        public static string GetQuantityLimitMessage(int limit) => $"Quantity must be at most {limit}.";
    }
}
