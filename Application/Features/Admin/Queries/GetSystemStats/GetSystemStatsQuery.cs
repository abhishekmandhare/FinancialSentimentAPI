using MediatR;

namespace Application.Features.Admin.Queries.GetSystemStats;

public record GetSystemStatsQuery : IRequest<SystemStatsDto>;
