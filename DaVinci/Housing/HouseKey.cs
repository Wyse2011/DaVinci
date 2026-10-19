using FFXIVClientStructs.FFXIV.Client.Game;

namespace DaVinci.Housing;

/// <summary>
/// Identifies one interior across all clients. Wraps the game's own 64-bit HouseId,
/// which already encodes world, district, ward, plot or apartment, and room.
/// </summary>
public readonly record struct HouseKey(ulong Id)
{
    private HouseId Raw => (HouseId)Id;

    public ushort WorldId => Raw.WorldId;
    public ushort TerritoryTypeId => Raw.TerritoryTypeId;
    public int Ward => Raw.WardIndex + 1;
    public short Room => Raw.RoomNumber;
    public bool IsApartment => Raw.IsApartment;
    public bool IsWorkshop => Raw.IsWorkshop;
    public int Plot => Raw.PlotIndex + 1;
    public int ApartmentWing => Raw.ApartmentDivision + 1;

    /// <summary>Same building, ignoring the room. Used to relate private chambers to their FC estate.</summary>
    public bool SameBuilding(HouseKey other) =>
        WorldId == other.WorldId && TerritoryTypeId == other.TerritoryTypeId && Raw.WardIndex == ((HouseId)other.Id).WardIndex
        && ((HouseId)Id).Unit.Value == ((HouseId)other.Id).Unit.Value;

    /// <summary>Safe file name and future sync channel id.</summary>
    public string Stem => Id.ToString("X16");

    public string Describe()
    {
        var where = IsApartment ? $"Apartment wing {ApartmentWing}" : $"Plot {Plot}";
        var room = IsWorkshop ? "workshop" : Room == 0 ? "main house" : $"room {Room}";
        return $"Territory {TerritoryTypeId}, Ward {Ward}, {where}, {room}";
    }

    public override string ToString() => Describe();

    public static bool IsValid(ulong id) => id != 0 && id != ulong.MaxValue;
}
