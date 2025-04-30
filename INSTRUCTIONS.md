# Pizzeria Order Processor - Running Instructions

## Prerequisites

- **.NET 9 SDK** installed on your machine

## Setup Instructions

1. **Clone or download** the Pizzeria Order Processor repository to your local machine
2. **Navigate** to the root directory of the project in your terminal or command prompt

## Building the Application

To build the application, run:

```powershell
dotnet build
```

This will restore all necessary NuGet packages and compile the application.

## Running the Application

### Process All Sample Orders (Default)

To process all sample orders automatically:

```powershell
dotnet run --project PizzeriaOrderProcessor
```

This will process all order files in the default location.

You can also explicitly specify all files with wildcards:

```powershell
dotnet run --project PizzeriaOrderProcessor -- "SampleOrders\*.json"
```

### Process a Specific Order File

To process a single specific order file:

```powershell
dotnet run --project PizzeriaOrderProcessor -- "SampleOrders\order21_valid.json"
```

### Process Multiple Specific Files

To process multiple selected files:

```powershell
dotnet run --project PizzeriaOrderProcessor -- "SampleOrders\order10_valid_complex.json" "SampleOrders\order21_valid.json"
```

### Process Orders from a Custom Location

To process orders from a different directory:

```powershell
dotnet run --project PizzeriaOrderProcessor -- "path\to\your\orders\*.json"
```

## Command-Line Arguments

The application accepts file paths as command-line arguments:

- You can use wildcards (`*.json`) to process multiple files
- You can specify absolute or relative paths
- Multiple paths can be provided, separated by spaces
- Paths with spaces should be enclosed in quotes

## Configuration

The application behavior can be customized by modifying the `appsettings.json` file. Key settings include:

- **FileSettings**: Paths to product and ingredient catalog files
- **VatSettings**: VAT rate configuration
- **ValidationSettings**: Order validation rules
- **TimeSettings**: Time-related constraints
- **OperatingHours**: Business hours for order delivery
- **QueueSettings**: Queue batch size configuration

## Understanding the Output

When you run the application, you'll see:

1. **Progress Information**: Details about files being processed
2. **Validation Results**: Lists of valid and invalid orders
3. **Calculation Summary**: Order totals and ingredient requirements
4. **Queue Processing**: Information about orders pushed to the queue
5. **Final Summary**: Overall processing statistics

## Troubleshooting

If you encounter issues:

- Check the log files in the `logs` directory for detailed error information
- Ensure your JSON files follow the expected format (see sample files for reference)
- Verify that the product and ingredient catalog files exist and are properly formatted
- Check that your order files meet the validation requirements in `ValidationSettings`

## Example Workflow

Here's a typical workflow:

1. Prepare your order JSON files following the format in the sample files
2. Run the application with the path to your files
3. Review the output to identify any validation issues
4. Check the logs for detailed information about any errors
5. Adjust your order files as needed and run again
