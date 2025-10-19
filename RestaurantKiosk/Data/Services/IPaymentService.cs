namespace RestaurantKiosk.Data.Services;

public interface IPaymentService
{
    Task<InvoiceResponse> CreateInvoiceAsync(
        string externalId,
        decimal amount,
        string customerName,
        string customerEmail,
        string description,
        string successRedirectUrl,
        string failureRedirectUrl);

    Task<EWalletChargeResponse> CreateGCashPaymentAsync(
        string externalId,
        decimal amount,
        string mobileNumber,
        string callbackUrl);

    Task<EWalletChargeResponse> CreateMayaPaymentAsync(
        string externalId,
        decimal amount,
        string mobileNumber,
        string callbackUrl);

    Task<InvoiceResponse> GetInvoiceAsync(string invoiceId);
    Task<EWalletChargeResponse> GetEWalletChargeAsync(string chargeId);
    bool VerifyWebhook(string webhookToken, string requestBody, string xenditSignature);
}

