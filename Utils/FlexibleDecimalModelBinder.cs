using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace FirebaseLoginCustom.Utils
{
    /// <summary>
    /// Model binder for decimal values that flexibly supports both standard InvariantCulture
    /// (dot as decimal separator, from JavaScript toFixed / HTML5 number inputs) and
    /// regional formats such as es-AR (comma as decimal separator, dot as thousands separator).
    /// </summary>
    public class FlexibleDecimalModelBinder : IModelBinder
    {
        public Task BindModelAsync(ModelBindingContext bindingContext)
        {
            if (bindingContext == null)
            {
                throw new ArgumentNullException(nameof(bindingContext));
            }

            var valueProviderResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
            if (valueProviderResult == ValueProviderResult.None)
            {
                return Task.CompletedTask;
            }

            var value = valueProviderResult.FirstValue;
            if (string.IsNullOrWhiteSpace(value))
            {
                return Task.CompletedTask;
            }

            if (TryParseFlexibleDecimal(value, out var decimalValue))
            {
                bindingContext.Result = ModelBindingResult.Success(decimalValue);
            }
            else
            {
                bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, "Formato de importe no válido.");
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Parses a decimal string considering both Argentine/European formats (1.500,50 / 30.000)
        /// and HTML5/JavaScript wire formats (30000.00 / 1500.50 / 30000).
        /// </summary>
        public static bool TryParseFlexibleDecimal(string? input, out decimal result)
        {
            result = 0m;
            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            var s = input.Trim();

            // When both '.' and ',' are present:
            if (s.Contains('.') && s.Contains(','))
            {
                if (s.LastIndexOf(',') > s.LastIndexOf('.'))
                {
                    // Argentine/European: 30.000,50 -> strip dots, comma becomes decimal point
                    s = s.Replace(".", "").Replace(",", ".");
                }
                else
                {
                    // US: 30,000.50 -> strip commas
                    s = s.Replace(",", "");
                }
                return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out result);
            }

            // When only ',' is present: e.g. 30000,50 or 30000,00
            if (s.Contains(','))
            {
                s = s.Replace(",", ".");
                return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out result);
            }

            // When only '.' is present:
            if (s.Contains('.'))
            {
                var parts = s.Split('.');
                if (parts.Length > 2)
                {
                    // Multiple dots: e.g. 1.000.000 (thousands separators)
                    s = s.Replace(".", "");
                    return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out result);
                }

                // Exactly one dot:
                // If part after dot has 3 digits and part before has 1-3 digits (e.g. 30.000, 5.000, 100.000),
                // in Argentine context this represents thousands grouping without decimals.
                if (parts[1].Length == 3 && parts[0].Length >= 1 && parts[0].Length <= 3)
                {
                    s = s.Replace(".", "");
                    return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out result);
                }

                // Otherwise it is standard decimal wire format: e.g. 30000.00, 1500.50, 30.5
                return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out result);
            }

            // Plain integer e.g. 30000
            return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out result);
        }
    }

    /// <summary>
    /// Provider to supply FlexibleDecimalModelBinder for all decimal and nullable decimal models across ASP.NET Core MVC.
    /// </summary>
    public class FlexibleDecimalModelBinderProvider : IModelBinderProvider
    {
        public IModelBinder? GetBinder(ModelBinderProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (context.Metadata.ModelType == typeof(decimal) || context.Metadata.ModelType == typeof(decimal?))
            {
                return new FlexibleDecimalModelBinder();
            }

            return null;
        }
    }
}
