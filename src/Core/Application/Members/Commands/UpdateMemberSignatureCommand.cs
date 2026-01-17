using FluentValidation;
using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Members.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Members.Commands;

public record UpdateMemberSignatureCommand(UpdateMemberSignatureRequest Request) : IRequest<Result>;

public class UpdateMemberSignatureCommandValidator : AbstractValidator<UpdateMemberSignatureCommand>
{
    public UpdateMemberSignatureCommandValidator()
    {
        RuleFor(x => x.Request.ChandaNo)
            .NotEmpty().WithMessage("ChandaNo is required");

        RuleFor(x => x.Request.SignatureUrl)
            .NotEmpty().WithMessage("Signature URL is required");
    }
}

public class UpdateMemberSignatureCommandHandler : IRequestHandler<UpdateMemberSignatureCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public UpdateMemberSignatureCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(UpdateMemberSignatureCommand request, CancellationToken cancellationToken)
    {
        var member = await _context.Members
            .FirstOrDefaultAsync(m => m.ChandaNo == request.Request.ChandaNo, cancellationToken);

        if (member == null)
        {
            return Result.Failure($"Member with ChandaNo '{request.Request.ChandaNo}' not found");
        }

        member.UpdateSignature(request.Request.SignatureUrl);

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success("Member signature updated successfully");
    }
}
