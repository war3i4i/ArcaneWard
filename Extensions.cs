using System;
using System.Collections.Generic;
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
    
    public static Dictionary<long, string> GetPermittedPlayers(this ArcaneWardComponent ward)
    {
        Dictionary<long, string> dic = new();
        int @int = ward._znet.m_zdo.GetInt("permitted");
        for (int i = 0; i < @int; i++)
        {
            long @long = ward._znet.m_zdo.GetLong("pu_id" + i);
            string @string = ward._znet.m_zdo.GetString("pu_name" + i);
            if (@long != 0L) dic[@long] = @string;
        }
        return dic;
    }

    public static HashSet<long> GetPermittedPlayers(this ZDO z)
    {
        HashSet<long> set = [];
        int @int = z.GetInt("permitted");
        for (int i = 0; i < @int; i++)
        {
            long @long = z.GetLong("pu_id" + i);
            if (@long != 0L) set.Add(@long);
        }
        return set;
    }
    
    public static Dictionary<long, string> GetPermittedPlayersDic(this ZDO z)
    {
        Dictionary<long, string> dic = new();
        int @int = z.GetInt("permitted");
        for (int i = 0; i < @int; i++)
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
    
    public static string GetName(this ZDO z)
    {
        return z.GetString("Name", "$kg_arcaneward");  
    }
    
    private static void SetPermittedPlayers(this ArcaneWardComponent ward, Dictionary<long, string> users)
    {
        ward._znet.m_zdo.Set("permitted", users.Count);
        int c = 0;
        foreach (var user in users)
        {
            ward._znet.m_zdo.Set("pu_id" + c, user.Key);
            ward._znet.m_zdo.Set("pu_name" + c, user.Value);
            ++c;
        }
        ward._znet.InvokeRPC(ZNetView.Everybody, "RPC_ResetCache", [JSON.ToJSON(users)]);
    }
    
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
    
    public static void AddPermitted(this ArcaneWardComponent ward, long playerID, string playerName)
    {
        var permittedPlayers = ward.GetPermittedPlayers();
        if (permittedPlayers.ContainsKey(playerID) || playerID == 0) return;
        permittedPlayers[playerID] = playerName;
        ward.SetPermittedPlayers(permittedPlayers);
    }

    public static Minimap.PinData GetCustomPin(Minimap.PinType type, Vector3 pos, float radius)
    {
        Minimap.PinData pinData = null;
        float num = 999999f;
        foreach (Minimap.PinData pinData2 in Minimap.instance.m_pins)
            if (pinData2.m_type == type)
            {
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
    
    public static void RemovePermitted(this ArcaneWardComponent ward, long playerID)
    {
        var permittedPlayers = ward.GetPermittedPlayers();
        if (!permittedPlayers.ContainsKey(playerID)) return;
        permittedPlayers.Remove(playerID);
        ward.SetPermittedPlayers(permittedPlayers);
    }
    
    public static bool HasFlagFast(this Protection protection, Protection flag)
    {
        return (protection & flag) != 0;
    } 
    
    
    [HarmonyPatch(typeof(Minimap), nameof(Minimap.Start))]
    private static class SetMaxPins
    {
        public static void Postfix(Minimap __instance)
        {
            if (__instance.m_visibleIconTypes.Length < 200)
            {
                __instance.m_visibleIconTypes = new bool[200];
                for (int i = 0; i < __instance.m_visibleIconTypes.Length; ++i)
                    __instance.m_visibleIconTypes[i] = true;
            }
        }
    }
}