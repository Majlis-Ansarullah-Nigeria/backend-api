using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Organizations.DTOs;
using ManagementApi.Domain.Entities;
using MediatR;

namespace ManagementApi.Application.Organizations.Commands;

public record CreateDilaCommand(CreateDilaRequest Request) : IRequest<Result<Guid>>;

public class CreateDilaCommandHandler : IRequestHandler<CreateDilaCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;

    public CreateDilaCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(CreateDilaCommand request, CancellationToken cancellationToken)
    {
        var dila = new Dila(request.Request.Name, request.Request.Code, request.Request.ZoneId);

        dila.Update(
            request.Request.Name,
            request.Request.Code,
            request.Request.Address,
            request.Request.ContactPerson,
            request.Request.PhoneNumber,
            request.Request.Email
        );

        _context.Dilas.Add(dila);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(dila.Id, "Dila created successfully");
    }
}
