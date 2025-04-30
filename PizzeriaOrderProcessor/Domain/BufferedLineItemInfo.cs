namespace PizzeriaOrderProcessor.Domain
{
    /// <summary>
    /// Represents a buffered raw order line item along with its original source file name.
    /// Requires access to RawOrderLineItem, which might need a using statement or namespace adjustment depending on RawOrderLineItem's location.
    /// Assuming RawOrderLineItem might be in Services.Validators or Domain itself.
    /// </summary>

    public record BufferedLineItemInfo(RawOrderLineItem LineItem, string SourceFileName);
}
