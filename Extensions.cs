using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using fastJSON;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace kg_ArcaneWard;

public static class Extensions
{
    public static string ToTime(this int seconds)
    {
        if (seconds == 0) return "0$kg_arcaneward_seconds".Localize();
        TimeSpan t = TimeSpan.FromSeconds(seconds);
        string result = "";
        if (t.Days > 0) result += $"{t.Days:D2}$kg_arcaneward_days ";
        if (t.Hours > 0) result += $"{t.Hours:D2}$kg_arcaneward_hours ";
        if (t.Minutes > 0) result += $"{t.Minutes:D2}$kg_arcaneward_minutes ";
        if (t.Seconds > 0) result += $"{t.Seconds:D2}$kg_arcaneward_seconds";
        return result.Localize().Trim();
    }

    public static string ToTimeNoS(this int seconds)
    {
        if (seconds == 0) return "0$kg_arcaneward_seconds".Localize();
        TimeSpan t = TimeSpan.FromSeconds(seconds);
        string result = "";
        if (t.Days > 0) result += $"{t.Days:D2}$kg_arcaneward_days ";
        if (t.Hours > 0) result += $"{t.Hours:D2}$kg_arcaneward_hours ";
        if (t.Minutes > 0) result += $"{t.Minutes:D2}$kg_arcaneward_minutes";
        return result.Localize().Trim();
    }

    public static HashSet<long> GetPermittedPlayersHashset(this ZDO z)
    {
        HashSet<long> set = [];
        int @int = z.GetInt("permitted");
        for (int i = 0; i < @int; ++i)
        {
            long @long = z.GetLong("pu_id" + i);
            if (@long != 0L) set.Add(@long);
        }

        return set;
    }

    public static Dictionary<long, string> GetPermittedPlayers(this ZDO z)
    {
        Dictionary<long, string> dic = new();
        int @int = z.GetInt("permitted");
        for (int i = 0; i < @int; ++i)
        {
            long @long = z.GetLong("pu_id" + i);
            string @string = z.GetString("pu_name" + i);
            if (@long != 0L) dic[@long] = @string;
        }

        return dic;
    }

    public static void RemoveAllChildrenExceptFirst(this Transform t)
    {
        for (int i = t.childCount - 1; i > 0; --i)
        {
            Object.DestroyImmediate(t.GetChild(i).gameObject);
        }
    }

    public static string GetName(this ZDO z) => z.GetString(ArcaneWardComponent._cache_Key_Name, "$kg_arcaneward");
    
    public static void SetPermittedPlayers(this ZDO ward, Dictionary<long, string> users)
    {
        ward.Set("permitted", users.Count);
        int c = 0;
        foreach (var user in users)
        {
            ward.Set("pu_id" + c, user.Key);
            ward.Set("pu_name" + c, user.Value);
            ++c;
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class ProtectionIconAttribute(string name) : Attribute
    {
        public string Name { get; } = name;
    }

    public static Sprite TryFindIcon(string name, Sprite defaultSprite = null)
    {
        if (string.IsNullOrEmpty(name)) return defaultSprite;
        if (ZNetScene.instance.GetPrefab(name) is not { } prefab) return defaultSprite;
        if (prefab.GetComponent<ItemDrop>() is { } item)
            return item.m_itemData.GetIcon();
        if (prefab.GetComponent<Piece>() is { } piece)
            return piece.m_icon;

        return defaultSprite;
    }

    public enum PinVisibility
    {
        Any,
        Visible,
        Hidden
    }

    public static Minimap.PinData GetCustomPin(Minimap.PinType type, Vector3 pos, float radius, PinVisibility visibility)
    {
        Minimap.PinData pinData = null;
        float num = 999999f;
        foreach (Minimap.PinData pinData2 in Minimap.instance.m_pins)
            if (pinData2.m_type == type)
            {
                if (visibility == PinVisibility.Visible && !Minimap.instance.m_visibleIconTypes[(int)pinData2.m_type]) continue;
                else if (visibility == PinVisibility.Hidden && Minimap.instance.m_visibleIconTypes[(int)pinData2.m_type]) continue;


                float num2 = Utils.DistanceXZ(pos, pinData2.m_pos);
                if (num2 < radius && (num2 < num || pinData == null))
                {
                    pinData = pinData2;
                    num = num2;
                }
            }

        return pinData;
    }

    public static string Localize(this string key) => Localization.instance.Localize(key);

    public static bool HasFlagFast(this Protection protection, Protection flag)
    {
        return (protection & flag) != 0;
    }

    [HarmonyPatch(typeof(Minimap), nameof(Minimap.Start))]
    private static class SetMaxPins
    {
        public static void ExpandArray(Minimap map)
        {
            if (map.m_visibleIconTypes.Length < 200)
            {
                map.m_visibleIconTypes = new bool[200];
                for (int i = 0; i < map.m_visibleIconTypes.Length; ++i) 
                    map.m_visibleIconTypes[i] = true;
            }
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            CodeMatcher matcher = new(instructions);
            var target = AccessTools.Method(typeof(Minimap), nameof(Minimap.Reset));
            matcher.MatchForward(false, new CodeMatch(OpCodes.Call, target));
            if (!matcher.IsValid) return instructions;
            matcher.Advance(1);
            var insert = AccessTools.Method(typeof(SetMaxPins), nameof(ExpandArray));
            matcher.Insert(new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Call, insert));
            return matcher.InstructionEnumeration();
        }
    }
}