using Api.Dtos;
using Api.Modules.Errors;
using Application.Common.Interfaces.Queries;
using Application.Rooms.Commands;
using Domain.Rooms;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("rooms")]
public sealed class RoomsController(ISender sender, IRoomQueries queries) : ControllerBase
{
    [HttpPost("filter")]
    public async Task<ActionResult<IReadOnlyList<RoomDto>>> FilterRooms(SearchRoomDto request,
        CancellationToken cancellationToken)
    {
        RoomType? type = null;
        if (request.Type is not null)
        {
            if (!Enum.TryParse<RoomType>(request.Type, true, out var parsed) || !Enum.IsDefined(parsed))
                return BadRequest("Unknown room type.");
            type = parsed;
        }
        var rooms = await queries.Filter(request.Number, request.FloorFrom, request.FloorTo,
            request.PricePerNightFrom, request.PricePerNightTo, type, request.IsAvailable,
            AsUtc(request.CreatedAtFrom), AsUtc(request.CreatedAtTo), cancellationToken);
        return Ok(rooms.Select(RoomDto.FromDomainModel).ToArray());
    }

    private static DateTime? AsUtc(DateTime? value) => value?.Kind switch
    {
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc),
        DateTimeKind.Local => value.Value.ToUniversalTime(),
        _ => value
    };

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RoomDto>>> GetRooms(CancellationToken cancellationToken) =>
        (await queries.GetAll(cancellationToken)).Select(RoomDto.FromDomainModel).ToArray();

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RoomDto>> GetRoom(Guid id, CancellationToken cancellationToken)
    {
        var room = await queries.GetById(new RoomId(id), cancellationToken);
        return room.Match<ActionResult<RoomDto>>(
            value => RoomDto.FromDomainModel(value), () => NotFound());
    }

    [HttpPost]
    public async Task<ActionResult<RoomDto>> CreateRoom(CreateRoomDto request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateRoomCommand(request.Number, request.Floor,
            request.PricePerNight, request.Type, request.IsAvailable), cancellationToken);
        return result.Match<ActionResult<RoomDto>>(
            room => CreatedAtAction(nameof(GetRoom), new { id = room.Id.Value },
                RoomDto.FromDomainModel(room)),
            error => error.ToObjectResult());
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<RoomDto>> UpdateRoom(Guid id, UpdateRoomDto request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new UpdateRoomCommand(id, request.Number, request.Floor,
            request.PricePerNight, request.Type, request.IsAvailable), cancellationToken);
        return result.Match<ActionResult<RoomDto>>(
            room => RoomDto.FromDomainModel(room), error => error.ToObjectResult());
    }
}
