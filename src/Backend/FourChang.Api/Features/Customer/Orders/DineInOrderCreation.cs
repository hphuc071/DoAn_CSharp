using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using FourChang.Presentation.Shared.Contracts.Customer;
using Microsoft.AspNetCore.WebUtilities;

namespace FourChang.Api.Features.Customer.Orders;

// Dormant UC06 transactional writer contract. Does not enable DINE_IN order
// creation without explicit business approval AND separate SQL fixture approval.
// No production defaults are provided for any of these switches.
public static class DineInOrderCreation
{
    public static bool Ready(IConfiguration configuration) =>
        FulfillmentPreviewService.On(configuration, "Orders:CustomerCreationEnabled") &&
        FulfillmentPreviewService.On(configuration, "Orders:SqlIntegrationValidated") &&
        FulfillmentPreviewService.On(configuration, "Orders:DineInCreationEnabled") &&
        FulfillmentPreviewService.On(configuration, "Orders:DineIn:BusinessApproved") &&
        FulfillmentPreviewService.On(configuration, "Orders:DineIn:SqlWriterValidated") &&
        FulfillmentPreviewService.On(configuration, "Orders:FulfillmentPreviewEnabled") &&
        FulfillmentPreviewService.On(configuration, "Orders:TableQrProtocolValidated") &&
        FulfillmentPreviewService.On(configuration, "Orders:TableQr:IssuanceSqlValidated");

    // Only a canonical 32-byte base64url token and the EXACT preview proof may
    // enter the writer. The QR protocol does not authenticate the Customer.
    public static string? Validate(DineInOrderCreateRequest? request)
    {
        if (request is null || request.Items is null || request.Items.Count is < 1 or > 50 ||
            request.Items.Any(i => i is null || i.MaCauHinhMon <= 0 ||
                i.SoLuong is < 1 or > 99 || i.PhanTramDuong is < 0 or > 100 ||
                i.PhanTramDa is < 0 or > 100 || (i.GhiChu?.Length ?? 0) > 500 ||
                (i.GhiChu?.Any(char.IsControl) ?? false) ||
                i.Toppings is null || i.Toppings.Count > 10 ||
                i.Toppings.Any(t => t is null || t.MaMonThem <= 0 || t.SoPhanMoiLy < 1)))
            return "DINE_IN_CART_INVALID";
        if ((request.GhiChu?.Length ?? 0) > 500 ||
            (request.GhiChu?.Any(char.IsControl) ?? false))
            return "DINE_IN_NOTE_INVALID";
        if (request.QrToken is null ||
            !Regex.IsMatch(request.QrToken, @"\A[A-Za-z0-9_-]{43}\z"))
            return "DINE_IN_QR_INVALID";
        try
        {
            var bytes = WebEncoders.Base64UrlDecode(request.QrToken);
            if (bytes.Length != 32 || WebEncoders.Base64UrlEncode(bytes) != request.QrToken)
                return "DINE_IN_QR_INVALID";
        }
        catch (FormatException) { return "DINE_IN_QR_INVALID"; }
        if (request.PreviewProof is null || request.PreviewProof.Length is < 20 or > 8192 ||
            !Regex.IsMatch(request.PreviewProof, @"\A[A-Za-z0-9_-]+\z"))
            return "DINE_IN_PROOF_INVALID";
        return null;
    }

    public static byte[] Hash(DineInOrderCreateRequest request) =>
        SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request));

    public static byte[] Digest(DineInOrderCreateRequest request) =>
        SHA256.HashData(WebEncoders.Base64UrlDecode(request.QrToken!));

    public static FulfillmentPreviewRequest ToPreview(DineInOrderCreateRequest request) =>
        new("DINE_IN", request.QrToken, null, null, null, null, null,
            request.Items, request.PreviewProof);
}
