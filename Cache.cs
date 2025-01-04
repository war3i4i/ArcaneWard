using HarmonyLib;
using UnityEngine;

namespace kg_ArcaneWard;

public static class Cache
{
    public static int CachedGuildId = -1;
    public static void RecacheGuildID()
    {
        CachedGuildId = -1;
        var ownGuild = Guilds.API.GetOwnGuild();
        if (ownGuild != null) CachedGuildId = ownGuild.General.id;
    } 
    [HarmonyPatch(typeof(Player),nameof(Player.SetLocalPlayer))]
    private static class Player_SetLocalPlayer_Patch
    {
        private static void Postfix(Player __instance) => RecacheGuildID();
    }
}