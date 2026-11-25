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

public sealed class RoomsControllerTests : BaseIntegrationTest, IAsyncLifetime
{
    private readonly Room _first;
    private readonly Room _second;
    private readonly string _route;

    public RoomsControllerTests(IntegrationTestWebFactory factory) : base(factory)
    {
        _first = RoomsTestData.FirstTestRoom();
        _second = RoomsTestData.SecondTestRoom();
        _route = "/rooms";
    }

    public async Task InitializeAsync()
    {
        Context.Rooms.AddRange(_first, _second);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
    }

    public async Task DisposeAsync()
    {
        Context.Rooms.RemoveRange(await Context.Rooms.ToListAsync());
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
    }

    private async Task<RoomDto[]> DatabaseSnapshot() => (await Context.Rooms.AsNoTracking().ToListAsync())
        .Select(RoomDto.FromDomainModel).ToArray();

    [Fact]
    public async Task Create_ValidRoom_ReturnsCreatedAndPersistsAllFields()
    {
        // Arrange
        var room = RoomsTestData.ThirdTestRoom();
        var request = new CreateRoomDto(room.Number, room.Floor, room.PricePerNight, room.Type, room.IsAvailable);
        // Act
        var response = await Client.PostAsJsonAsync(_route, request, JsonOptions);
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = (await response.Content.ReadFromJsonAsync<RoomDto>(JsonOptions))!;
        dto.Id.Should().NotBeEmpty();
        dto.Should().BeEquivalentTo(request);
        dto.CreatedAt.Should().NotBe(default);
        var stored = await Context.Rooms.AsNoTracking().SingleAsync(x => x.Id == new RoomId(dto.Id));
        RoomDto.FromDomainModel(stored).Should().BeEquivalentTo(dto,
            options => options.Excluding(x => x.CreatedAt));
        stored.CreatedAt.Should().BeCloseTo(dto.CreatedAt, TimeSpan.FromMicroseconds(1));
        (await DatabaseSnapshot()).Should().HaveCount(3);
        response.Headers.Location.Should().NotBeNull();
    }

    [Fact]
    public async Task Create_DuplicateNumber_ReturnsConflictAndPreservesOriginal()
    {
        // Arrange
        var before = await DatabaseSnapshot();
        var request = new CreateRoomDto(_first.Number, 10, 999m, RoomType.Suite, false);
        // Act
        var response = await Client.PostAsJsonAsync(_route, request, JsonOptions);
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await DatabaseSnapshot()).Should().BeEquivalentTo(before);
        var matches = await Context.Rooms.AsNoTracking().Where(x => x.Number == _first.Number).ToListAsync();
        matches.Should().ContainSingle().Which.Id.Should().Be(_first.Id);
    }

    [Theory]
    [InlineData("", 1, 100)]
    [InlineData("123456789012345678901", 1, 100)]
    [InlineData("New-room", -1, 100)]
    [InlineData("New-room", 201, 100)]
    [InlineData("New-room", 1, 0)]
    [InlineData("New-room", 1, 1000001)]
    public async Task Create_InvalidData_ReturnsBadRequestAndDoesNotInsert(string number, int floor, int price)
    {
        // Arrange
        var before = await DatabaseSnapshot();
        var request = new CreateRoomDto(number, floor, price, RoomType.Standard, true);
        // Act
        var response = await Client.PostAsJsonAsync(_route, request, JsonOptions);
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DatabaseSnapshot()).Should().BeEquivalentTo(before);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Update_ValidData_IncludingUnchangedNumber_PersistsAllFields(bool keepNumber)
    {
        // Arrange
        var before = await DatabaseSnapshot();
        var request = new UpdateRoomDto(keepNumber ? _first.Number : "Updated-900", 9, 999m, RoomType.Family, false);
        // Act
        var response = await Client.PutAsJsonAsync($"{_route}/{_first.Id}", request, JsonOptions);
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = (await response.Content.ReadFromJsonAsync<RoomDto>(JsonOptions))!;
        dto.Should().BeEquivalentTo(request);
        dto.Id.Should().Be(_first.Id.Value);
        var after = await DatabaseSnapshot();
        after.Single(x => x.Id == dto.Id).Should().BeEquivalentTo(dto);
        dto.CreatedAt.Should().Be(before.Single(x => x.Id == dto.Id).CreatedAt);
        after.Single(x => x.Id == _second.Id.Value).Should().BeEquivalentTo(before.Single(x => x.Id == _second.Id.Value));
    }

    [Fact]
    public async Task Update_NumberOwnedByAnotherRoom_ReturnsConflictAndDoesNotChangeDatabase()
    {
        // Arrange
        var before = await DatabaseSnapshot();
        var request = new UpdateRoomDto(_second.Number, 9, 999m, RoomType.Family, false);
        // Act
        var response = await Client.PutAsJsonAsync($"{_route}/{_first.Id}", request, JsonOptions);
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await DatabaseSnapshot()).Should().BeEquivalentTo(before);
    }

    [Fact]
    public async Task Update_UnknownId_ReturnsNotFoundAndDoesNotChangeDatabase()
    {
        // Arrange
        var before = await DatabaseSnapshot();
        var request = new UpdateRoomDto("Unknown-999", 9, 999m, RoomType.Family, false);
        // Act
        var response = await Client.PutAsJsonAsync($"{_route}/{Guid.NewGuid()}", request, JsonOptions);
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await DatabaseSnapshot()).Should().BeEquivalentTo(before);
    }

    [Theory]
    [InlineData("", 1, 100)]
    [InlineData("123456789012345678901", 1, 100)]
    [InlineData("Changed", -1, 100)]
    [InlineData("Changed", 201, 100)]
    [InlineData("Changed", 1, 0)]
    [InlineData("Changed", 1, 1000001)]
    public async Task Update_InvalidData_ReturnsBadRequestAndDoesNotChangeDatabase(string number, int floor, int price)
    {
        // Arrange
        var before = await DatabaseSnapshot();
        var request = new UpdateRoomDto(number, floor, price, RoomType.Suite, false);
        // Act
        var response = await Client.PutAsJsonAsync($"{_route}/{_first.Id}", request, JsonOptions);
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DatabaseSnapshot()).Should().BeEquivalentTo(before);
    }

    [Fact]
    public async Task GetAll_ReturnsExactlySeededRooms()
    {
        // Arrange
        var expected = await DatabaseSnapshot();
        // Act
        var response = await Client.GetAsync(_route);
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<RoomDto[]>(JsonOptions);
        result.Should().BeEquivalentTo(expected);
        result!.Select(x => x.Id).Should().BeEquivalentTo(new[] { _first.Id.Value, _second.Id.Value });
    }

    [Fact]
    public async Task GetById_ReturnsStoredRoom()
    {
        // Arrange
        var expected = (await DatabaseSnapshot()).Single(x => x.Id == _first.Id.Value);
        // Act
        var response = await Client.GetAsync($"{_route}/{_first.Id}");
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<RoomDto>(JsonOptions)).Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GetById_UnknownId_ReturnsNotFound()
    {
        // Arrange
        var before = await DatabaseSnapshot();
        var id = Guid.NewGuid();
        // Act
        var response = await Client.GetAsync($"{_route}/{id}");
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await DatabaseSnapshot()).Should().BeEquivalentTo(before);
    }
}
