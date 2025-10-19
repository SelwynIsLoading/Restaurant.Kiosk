namespace RestaurantKiosk.Data.Services;

public interface ICurrencyService
{
    string FormatPeso(decimal amount);
    string FormatPesoWithSeparator(decimal amount);
    decimal VatRate { get; }
    decimal CalculateVat(decimal subtotal);
    decimal CalculateTotal(decimal subtotal);
}

