namespace RestaurantKiosk.Data.Services;

public class CurrencyService : ICurrencyService
{
    /// <summary>
    /// Gets the Philippine VAT rate (12%)
    /// </summary>
    public decimal VatRate => 0.12m;

    /// <summary>
    /// Formats a decimal value as Philippine Peso currency
    /// </summary>
    /// <param name="amount">The amount to format</param>
    /// <returns>Formatted string with ₱ symbol</returns>
    public string FormatPeso(decimal amount)
    {
        return $"₱{amount:F2}";
    }

    /// <summary>
    /// Formats a decimal value as Philippine Peso currency with thousands separator
    /// </summary>
    /// <param name="amount">The amount to format</param>
    /// <returns>Formatted string with ₱ symbol and thousands separator</returns>
    public string FormatPesoWithSeparator(decimal amount)
    {
        return $"₱{amount:N2}";
    }

    /// <summary>
    /// Calculates VAT amount for a given subtotal
    /// </summary>
    /// <param name="subtotal">The subtotal amount</param>
    /// <returns>VAT amount</returns>
    public decimal CalculateVat(decimal subtotal)
    {
        return subtotal * VatRate;
    }

    /// <summary>
    /// Calculates total amount including VAT
    /// </summary>
    /// <param name="subtotal">The subtotal amount</param>
    /// <returns>Total amount including VAT</returns>
    public decimal CalculateTotal(decimal subtotal)
    {
        return subtotal + CalculateVat(subtotal);
    }
}
