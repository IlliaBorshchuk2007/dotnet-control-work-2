using Domain.Rooms;
using LanguageExt;

namespace Application.Common.Interfaces.Queries;

public interface IRoomQueries
{
    Task<IReadOnlyList<Room>> Filter(string? number, int? floorFrom, int? floorTo,
        decimal? pricePerNightFrom, decimal? pricePerNightTo, RoomType? type,
        bool? isAvailable, DateTime? createdAtFrom, DateTime? createdAtTo,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<Room>> GetAll(CancellationToken cancellationToken);
    Task<Option<Room>> GetById(RoomId id, CancellationToken cancellationToken);
}
