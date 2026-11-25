using Domain.Rooms;

namespace Tests.Data;

public static class RoomsTestData
{
    public static Room FirstTestRoom() => Room.New(RoomId.New(), "North-101", 1, 100m, RoomType.Standard, true);
    public static Room SecondTestRoom() => Room.New(RoomId.New(), "North-202", 2, 200m, RoomType.Deluxe, false);
    public static Room ThirdTestRoom() => Room.New(RoomId.New(), "South-303", 3, 300m, RoomType.Suite, true);
    public static Room FourthTestRoom() => Room.New(RoomId.New(), "West-404", 4, 400m, RoomType.Family, false);
}
