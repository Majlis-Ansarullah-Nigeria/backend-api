using FluentValidation;
using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Members.DTOs;
using ManagementApi.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Members.Commands;

public record UpdateMemberPhotoCommand(UpdateMemberPhotoRequest Request) : IRequest<Result>;

public class UpdateMemberPhotoCommandValidator : AbstractValidator<UpdateMemberPhotoCommand>
{
    public UpdateMemberPhotoCommandValidator()
    {
        RuleFor(x => x.Request.ChandaNo)
            .NotEmpty().WithMessage("ChandaNo is required");

        RuleFor(x => x.Request.PhotoUrl)
            .NotEmpty().WithMessage("Photo URL is required");
    }
}

public class UpdateMemberPhotoCommandHandler : IRequestHandler<UpdateMemberPhotoCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public UpdateMemberPhotoCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(UpdateMemberPhotoCommand request, CancellationToken cancellationToken)
    {
        var member = await _context.Members
            .FirstOrDefaultAsync(m => m.ChandaNo == request.Request.ChandaNo, cancellationToken);

        if (member == null)
        {
            return Result.Failure($"Member with ChandaNo '{request.Request.ChandaNo}' not found");
        }

        member.UpdatePhotoUrl(request.Request.PhotoUrl);

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success("Member photo updated successfully");
    }
}
