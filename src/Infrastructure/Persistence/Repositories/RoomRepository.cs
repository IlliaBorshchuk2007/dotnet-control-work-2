using Application.Common.Interfaces.Queries;
using Application.Common.Interfaces.Repositories;
using Domain.Rooms;
using LanguageExt;
using Microsoft.EntityFrameworkCore;
using static LanguageExt.Prelude;

namespace Infrastructure.Persistence.Repositories;

public sealed class RoomRepository(ApplicationDbContext context) : IRoomRepository, IRoomQueries
{
    public async Task<IReadOnlyList<Room>> Filter(string? number, int? floorFrom, int? floorTo,
        decimal? pricePerNightFrom, decimal? pricePerNightTo, RoomType? type,
        bool? isAvailable, DateTime? createdAtFrom, DateTime? createdAtTo,
        CancellationToken cancellationToken)
    {
        IQueryable<Room> query = context.Rooms.AsNoTracking();
        if (number is not null)
        {
            // Treat SQL LIKE wildcards as literal characters entered by the user.
            var pattern = "%" + number.Replace("\\", "\\\\").Replace("%", "\\%")
                .Replace("_", "\\_") + "%";
            query = query.Where(room => EF.Functions.ILike(room.Number, pattern, "\\"));
        }
        if (floorFrom.HasValue) query = query.Where(room => room.Floor >= floorFrom.Value);
        if (floorTo.HasValue) query = query.Where(room => room.Floor <= floorTo.Value);
        if (pricePerNightFrom.HasValue) query = query.Where(room => room.PricePerNight >= pricePerNightFrom.Value);
        if (pricePerNightTo.HasValue) query = query.Where(room => room.PricePerNight <= pricePerNightTo.Value);
        if (type.HasValue) query = query.Where(room => room.Type == type.Value);
        if (isAvailable.HasValue) query = query.Where(room => room.IsAvailable == isAvailable.Value);
        if (createdAtFrom.HasValue) query = query.Where(room => room.CreatedAt >= createdAtFrom.Value);
        if (createdAtTo.HasValue) query = query.Where(room => room.CreatedAt <= createdAtTo.Value);
        return await query.OrderBy(room => room.Number).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Room>> GetAll(CancellationToken cancellationToken) =>
        await context.Rooms.AsNoTracking().OrderBy(x => x.Number).ToListAsync(cancellationToken);

    async Task<Option<Room>> IRoomQueries.GetById(RoomId id, CancellationToken cancellationToken) =>
        Optional(await context.Rooms.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken));

    public async Task<Option<Room>> GetById(RoomId id, CancellationToken cancellationToken) =>
        Optional(await context.Rooms.FirstOrDefaultAsync(x => x.Id == id, cancellationToken));

    public async Task<Option<Room>> GetByNumber(string number, CancellationToken cancellationToken) =>
        Optional(await context.Rooms.FirstOrDefaultAsync(x => x.Number == number, cancellationToken));

    public async Task<Room> Add(Room room, CancellationToken cancellationToken)
    {
        await context.Rooms.AddAsync(room, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        return room;
    }

    public async Task<Room> Update(Room room, CancellationToken cancellationToken)
    {
        context.Rooms.Update(room);
        await context.SaveChangesAsync(cancellationToken);
        return room;
    }
}
