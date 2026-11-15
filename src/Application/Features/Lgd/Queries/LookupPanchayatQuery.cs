using FluentValidation;
using GPMH.Application.Common.Exceptions;
using GPMH.Application.Common.Interfaces;
using GPMH.Application.DTOs.Lgd;
using MediatR;

namespace GPMH.Application.Features.Lgd.Queries;

public sealed record LookupPanchayatQuery(string LgdCode) : IRequest<LgdPanchayatDetailDto>;

public sealed class LookupPanchayatQueryValidator : AbstractValidator<LookupPanchayatQuery>
{
    public LookupPanchayatQueryValidator()
    {
        RuleFor(x => x.LgdCode)
            .NotEmpty().WithMessage("LGD code is required.")
            .Matches(@"^\d{5,8}$").WithMessage("LGD code must be 5 to 8 digits.");
    }
}

public sealed class LookupPanchayatQueryHandler(ILgdDirectoryClient lgdClient)
    : IRequestHandler<LookupPanchayatQuery, LgdPanchayatDetailDto>
{
    public async Task<LgdPanchayatDetailDto> Handle(LookupPanchayatQuery request, CancellationToken cancellationToken)
    {
        LgdPanchayatDetailDto? detail = await lgdClient
            .GetPanchayatByLgdCodeAsync(request.LgdCode, cancellationToken)
            .ConfigureAwait(false);

        return detail ?? throw new NotFoundException("GramPanchayat", request.LgdCode);
    }
}

