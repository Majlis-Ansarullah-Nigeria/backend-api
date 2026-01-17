using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Organizations.DTOs;
using MediatR;

namespace ManagementApi.Application.Organizations.Commands;

public record UpdateDilaCommand(UpdateDilaRequest Request) : IRequest<Result<Guid>>;

public class UpdateDilaCommandHandler : IRequestHandler<UpdateDilaCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;

    public UpdateDilaCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(UpdateDilaCommand request, CancellationToken cancellationToken)
    {
        var dila = await _context.Dilas.FindAsync(new object[] { request.Request.Id }, cancellationToken);

        if (dila == null)
        {
            return Result<Guid>.Failure("Dila not found");
        }

        dila.Update(
            request.Request.Name,
            request.Request.Code,
            request.Request.Address,
            request.Request.ContactPerson,
            request.Request.PhoneNumber,
            request.Request.Email
        );

        if (request.Request.ZoneId.HasValue)
        {
            dila.AssignToZone(request.Request.ZoneId.Value);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(dila.Id, "Dila updated successfully");
    }
}
