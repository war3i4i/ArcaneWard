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
    [HarmonyPatch(typeof(Game),nameof(Game.Awake))]
    private static class Game_Start_Patch
    {
        private static void Postfix(Game __instance) => CachedGuildId = -1;
    }
}