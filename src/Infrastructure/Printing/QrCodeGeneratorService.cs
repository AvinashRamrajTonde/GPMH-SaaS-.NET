using GPMH.Application.Common.Interfaces;
using QRCoder;

namespace GPMH.Infrastructure.Printing;

public sealed class QrCodeGeneratorService : IQrCodeGeneratorService
{
    public string GenerateSvg(string payload, int pixelsPerModule = 4)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);

        using QRCodeGenerator generator = new();
        using QRCodeData qrData = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
        using SvgQRCode svgQr = new(qrData);
        return svgQr.GetGraphic(pixelsPerModule, "#0c3a36", "#ffffff", drawQuietZones: true);
    }

    public byte[] GeneratePng(string payload, int pixelsPerModule = 6)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);

        using QRCodeGenerator generator = new();
        using QRCodeData qrData = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
        using PngByteQRCode pngQr = new(qrData);
        return pngQr.GetGraphic(pixelsPerModule);
    }
}

