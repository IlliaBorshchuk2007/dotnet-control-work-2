using System.Net;
using System.Net.Http.Json;
using Api.Dtos;
using Domain.Rooms;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Tests.Common;
using Tests.Data;
using Xunit;

namespace Api.Tests.Integration;

public sealed class RoomsFilterTests : BaseIntegrationTest, IAsyncLifetime
{
    private readonly Room[] _rooms;
    private readonly string _route;
    private RoomDto[] _stored = [];

    public RoomsFilterTests(IntegrationTestWebFactory factory) : base(factory)
    {
        // CreatedAt is set by the domain factory. Separate creation instants without changing private fields.
        var factories = new Func<Room>[] { RoomsTestData.FirstTestRoom, RoomsTestData.SecondTestRoom,
            RoomsTestData.ThirdTestRoom, RoomsTestData.FourthTestRoom };
        _rooms = new Room[factories.Length];
        for (var i = 0; i < factories.Length; i++)
        {
            if (i > 0) SpinWait.SpinUntil(() => DateTime.UtcNow >= _rooms[i - 1].CreatedAt.AddMilliseconds(2));
            _rooms[i] = factories[i]();
        }
        _route = "/rooms/filter";
    }

    public async Task InitializeAsync()
    {
        Context.Rooms.AddRange(_rooms);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
        _stored = (await Context.Rooms.AsNoTracking().OrderBy(x => x.Floor).ToListAsync())
            .Select(RoomDto.FromDomainModel).ToArray();
    }

    public async Task DisposeAsync()
    {
        Context.Rooms.RemoveRange(await Context.Rooms.ToListAsync());
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
    }

    private async Task AssertFilter(SearchRoomDto filter, int[] indices, bool allowAllOrEmpty = false)
    {
        // Arrange
        var expected = indices.Select(i => _stored[i]).ToArray();
        if (!allowAllOrEmpty)
        {
            expected.Should().NotBeEmpty();
            expected.Length.Should().BeLessThan(_stored.Length);
        }
        // Act
        var response = await Client.PostAsJsonAsync(_route, filter, JsonOptions);
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var actual = (await response.Content.ReadFromJsonAsync<RoomDto[]>(JsonOptions))!;
        actual.Select(x => x.Id).Should().BeEquivalentTo(expected.Select(x => x.Id));
        actual.Should().BeEquivalentTo(expected);
        var database = (await Context.Rooms.AsNoTracking().ToListAsync()).Select(RoomDto.FromDomainModel);
        database.Should().BeEquivalentTo(_stored);
    }

    [Fact] public Task Number_PartialMatch() => AssertFilter(new(Number: "orth"), [0, 1]);
    [Fact] public Task Number_IgnoresCase() => AssertFilter(new(Number: "nOrTh"), [0, 1]);
    [Fact] public Task FloorFrom_Inclusive() => AssertFilter(new(FloorFrom: 3), [2, 3]);
    [Fact] public Task FloorTo_Inclusive() => AssertFilter(new(FloorTo: 2), [0, 1]);
    [Fact] public Task PricePerNightFrom_Inclusive() => AssertFilter(new(PricePerNightFrom: 300m), [2, 3]);
    [Fact] public Task PricePerNightTo_Inclusive() => AssertFilter(new(PricePerNightTo: 200m), [0, 1]);
    [Fact] public Task Type_ExactMatch() => AssertFilter(new(Type: "Deluxe"), [1]);
    [Fact] public Task IsAvailable_True() => AssertFilter(new(IsAvailable: true), [0, 2]);
    [Fact] public Task IsAvailable_False() => AssertFilter(new(IsAvailable: false), [1, 3]);
    [Fact] public Task CreatedAtFrom_Inclusive() => AssertFilter(new(CreatedAtFrom: _stored[2].CreatedAt), [2, 3]);
    [Fact] public Task CreatedAtTo_Inclusive() => AssertFilter(new(CreatedAtTo: _stored[1].CreatedAt), [0, 1]);
    [Fact] public Task Floor_BothBounds() => AssertFilter(new(FloorFrom: 2, FloorTo: 3), [1, 2]);
    [Fact] public Task Price_BothBounds() => AssertFilter(new(PricePerNightFrom: 200m, PricePerNightTo: 300m), [1, 2]);
    [Fact] public Task CreatedAt_BothBounds() => AssertFilter(new(CreatedAtFrom: _stored[1].CreatedAt, CreatedAtTo: _stored[2].CreatedAt), [1, 2]);
    [Fact] public Task CreatedAt_EqualBounds() => AssertFilter(new(CreatedAtFrom: _stored[1].CreatedAt, CreatedAtTo: _stored[1].CreatedAt), [1]);
    [Fact] public Task CreatedAt_UnspecifiedMeansUtc() => AssertFilter(new(CreatedAtFrom: DateTime.SpecifyKind(_stored[2].CreatedAt, DateTimeKind.Unspecified)), [2, 3]);
    [Fact] public Task MultipleParameters_UseLogicalAnd() => AssertFilter(new(Number: "north", FloorFrom: 2, PricePerNightTo: 300m, Type: "Deluxe", IsAvailable: false), [1]);
    [Fact] public Task NoParameters_ReturnsAll() => AssertFilter(new(), [0, 1, 2, 3], true);
    [Fact] public Task NoMatches_ReturnsEmpty() => AssertFilter(new(Number: "missing"), [], true);
    [Fact] public Task SqlWildcard_IsLiteral() => AssertFilter(new(Number: "%"), [], true);
    [Fact] public Task ReversedRange_ReturnsEmpty() => AssertFilter(new(FloorFrom: 4, FloorTo: 1), [], true);

    [Fact]
    public async Task InvalidType_ReturnsBadRequest()
    {
        // Arrange
        var request = new SearchRoomDto(Type: "Unknown");
        // Act
        var response = await Client.PostAsJsonAsync(_route, request, JsonOptions);
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Context.Rooms.AsNoTracking().ToListAsync()).Select(RoomDto.FromDomainModel).Should().BeEquivalentTo(_stored);
    }
}
