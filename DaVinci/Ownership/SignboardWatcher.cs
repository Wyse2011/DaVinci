using System;
using System.Collections.Generic;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Network;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using DaVinci.Housing;

namespace DaVinci.Ownership;

/// <summary>What the game told us about an estate's placard. Kept in memory only, never saved or sent.</summary>
public sealed record SignboardInfo(
    HouseKey House,
    byte EstateType,
    byte Size,
    string EstateName,
    string OwnerName,
    string FcTag,
    string Greeting,
    DateTime SeenAtUtc);

/// <summary>
/// Passively observes estate placard data as the game receives it (when you view a placard,
/// and possibly when entering an estate; Phase 0 finds out which). It never requests anything.
/// This is the candidate source for visitor-side owner verification (permission layer 4).
/// </summary>
public sealed unsafe class SignboardWatcher : IDisposable
{
    private delegate void ReadPacketDelegate(AgentHousingSignboard* agent, HousingSignboardPacket* packet);

    private readonly Hook<ReadPacketDelegate>? hook;
    private readonly Dictionary<ulong, SignboardInfo> seen = new();

    public bool HookActive => hook is { IsEnabled: true };
    public IReadOnlyCollection<SignboardInfo> Seen => seen.Values;

    public SignboardWatcher(IGameInteropProvider interop)
    {
        try
        {
            hook = interop.HookFromAddress<ReadPacketDelegate>(
                (nint)AgentHousingSignboard.MemberFunctionPointers.ReadPacket, Detour);
            hook.Enable();
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Could not hook the housing signboard; owner probe disabled.");
        }
    }

    public SignboardInfo? Get(HouseKey house)
    {
        if (seen.TryGetValue(house.Id, out var exact))
            return exact;

        // Placards describe the estate, so a room inside it maps to its building's entry.
        foreach (var info in seen.Values)
            if (info.House.SameBuilding(house))
                return info;
        return null;
    }

    private void Detour(AgentHousingSignboard* agent, HousingSignboardPacket* packet)
    {
        try
        {
            if (packet != null && HouseKey.IsValid(packet->HouseId.Id))
            {
                var info = new SignboardInfo(
                    new HouseKey(packet->HouseId.Id),
                    packet->EstateType,
                    packet->Size,
                    packet->NameString,
                    packet->OwnerNameString,
                    packet->FCTagString,
                    packet->GreetingString,
                    DateTime.UtcNow);
                seen[info.House.Id] = info;
                Plugin.Log.Information($"[Signboard] {info.House} estateType={info.EstateType} size={info.Size} " +
                                       $"hasOwner={info.OwnerName.Length > 0} fcTag={(info.FcTag.Length > 0 ? "yes" : "no")}");
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Signboard detour failed");
        }

        hook!.Original(agent, packet);
    }

    public void Dispose() => hook?.Dispose();
}
