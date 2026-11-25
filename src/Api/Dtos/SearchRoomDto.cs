namespace Api.Dtos;

public sealed record SearchRoomDto(
    string? Number = null,
    int? FloorFrom = null,
    int? FloorTo = null,
    decimal? PricePerNightFrom = null,
    decimal? PricePerNightTo = null,
    string? Type = null,
    bool? IsAvailable = null,
    DateTime? CreatedAtFrom = null,
    DateTime? CreatedAtTo = null);
