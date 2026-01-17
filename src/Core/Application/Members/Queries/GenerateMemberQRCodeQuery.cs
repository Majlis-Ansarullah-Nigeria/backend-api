using FluentValidation;
using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using System.Text.Json;

namespace ManagementApi.Application.Members.Queries;

public record GenerateMemberQRCodeQuery(string ChandaNo) : IRequest<Result<byte[]>>;

public class GenerateMemberQRCodeQueryValidator : AbstractValidator<GenerateMemberQRCodeQuery>
{
    public GenerateMemberQRCodeQueryValidator()
    {
        RuleFor(x => x.ChandaNo)
            .NotEmpty().WithMessage("ChandaNo is required");
    }
}

public class GenerateMemberQRCodeQueryHandler : IRequestHandler<GenerateMemberQRCodeQuery, Result<byte[]>>
{
    private readonly IApplicationDbContext _context;

    public GenerateMemberQRCodeQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<byte[]>> Handle(GenerateMemberQRCodeQuery request, CancellationToken cancellationToken)
    {
        var member = await _context.Members
            .Include(m => m.Muqam)
            .FirstOrDefaultAsync(m => m.ChandaNo == request.ChandaNo, cancellationToken);

        if (member == null)
        {
            return Result<byte[]>.Failure($"Member with ChandaNo '{request.ChandaNo}' not found");
        }

        // Create QR code data with member information
        var qrData = new
        {
            ChandaNo = member.ChandaNo,
            Name = $"{member.FirstName} {member.MiddleName} {member.Surname}".Trim(),
            Email = member.Email,
            PhoneNo = member.PhoneNo,
            Muqam = member.Muqam?.Name,
            DateOfBirth = member.DateOfBirth?.ToString("yyyy-MM-dd")
        };

        var jsonData = JsonSerializer.Serialize(qrData);

        // Generate QR code
        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(jsonData, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(qrCodeData);
        var qrCodeImage = qrCode.GetGraphic(20);

        return Result<byte[]>.Success(qrCodeImage, "QR code generated successfully");
    }
}
