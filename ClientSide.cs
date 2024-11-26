using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Audio;

namespace kg_ArcaneWard;

public static class ClientSide
{
    private static readonly Dictionary<Minimap.PinData, ZDO> _pins = new();
    private const Minimap.PinType PINTYPEWARD = (Minimap.PinType)176;
    [HarmonyPatch(typeof(Game), nameof(Game.Start))] static class Game_Start_Patch { static void Postfix() => ArcaneWardComponent._canPlaceWard = false; }
    [HarmonyPatch(typeof(Player), nameof(Player.PlacePiece))]
    static class PlacePiece_Patch
    {
        static bool Prefix(Piece piece)
        {
            if (!piece.GetComponent<ArcaneWardComponent>()) return true;
            if (ArcaneWardComponent._canPlaceWard || Player.m_debugMode || ZNet.instance.IsServer()) return true;
            MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, "<color=red>$kg_arcanewardlimit</color>");
            return false;
        }
    }
    [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
    private static class ZNetScene_Awake_Patch
    {
        private static void Postfix(ZNetScene __instance)
        {
            ZRoutedRpc.instance.Register("ArcaneWard Data", new Action<long, bool>(ReceiveData_ArcaneWard));
            ZRoutedRpc.instance.Register("ArcaneWard Notify", new Action<long, string>(ReceiveData_Notify)); 
            List<GameObject> hammer = __instance.GetPrefab("Hammer").GetComponent<ItemDrop>().m_itemData.m_shared.m_buildPieces.m_pieces;
            if (!hammer.Contains(ArcaneWard.ArcaneWard_Piece)) hammer.Add(ArcaneWard.ArcaneWard_Piece);
            __instance.m_namedPrefabs.Add(ArcaneWard.ArcaneWard_Piece.name.GetStableHashCode(), ArcaneWard.ArcaneWard_Piece);
        }

        private static void ReceiveData_Notify(long sender, string wardName)
        {
            MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, $"Something triggered <color=green>{wardName}</color>");
        }

        private static void ReceiveData_ArcaneWard(long sender, bool can) => ArcaneWardComponent._canPlaceWard = can;
    }
    [HarmonyPatch(typeof(AudioMan), nameof(AudioMan.Awake))]
    private static class AudioMan_Awake_Patch
    {
        private static void Postfix(AudioMan __instance)
        {
            AudioMixerGroup SFXgroup = __instance.m_masterMixer.FindMatchingGroups("SFX")[0];
            foreach (GameObject go in ArcaneWard.Asset.LoadAllAssets<GameObject>())
            {
                foreach (AudioSource audioSource in go.GetComponentsInChildren<AudioSource>(true))
                    audioSource.outputAudioMixerGroup = SFXgroup;
            }
        }
    }
    [HarmonyPatch(typeof(Minimap), nameof(Minimap.GetSprite))]
    private static class Minimap_GetSprite_Patch
    {
        private static void Postfix(Minimap.PinType type, ref Sprite __result) => __result = type is PINTYPEWARD ? ArcaneWard.ArcaneWard_Icon : __result;
    }
    [HarmonyPatch(typeof(Minimap), nameof(Minimap.SetMapMode))]
    private static class Wards_MapControllerPatch
    {
        private static void CreatePins()
        {
            foreach (KeyValuePair<Minimap.PinData, ZDO> pin in _pins) Minimap.instance.RemovePin(pin.Key);
            _pins.Clear();
            List<ZDO> AllWards = [];
            int index = 0;
            while (!ZDOMan.instance.GetAllZDOsWithPrefabIterative(ServerSide.toSearch, AllWards, ref index)) { }
            AllWards.RemoveAll(zdo => !zdo.GetPermittedPlayersHashset().Contains(Player.m_localPlayer.GetPlayerID()));
            for (var i = 0; i < AllWards.Count; ++i)
            {
                var zdo = AllWards[i];
                if (!zdo.IsValid()) continue;
                string name = zdo.GetName();
                float fuel = zdo.GetFloat("Fuel");
                bool isActivated = zdo.GetBool("Enabled");
                string colorName = isActivated && fuel > 0 ? "<color=green>" : "<color=red>";
                Minimap.PinData pinData = new Minimap.PinData
                {
                    m_type = PINTYPEWARD,
                    m_name = $"{colorName}{name}</color>",
                    m_pos = zdo.GetPosition(),
                };
                if (!string.IsNullOrEmpty(pinData.m_name))
                {
                    pinData.m_NamePinData = new Minimap.PinNameData(pinData);
                }

                pinData.m_icon = ArcaneWard.ArcaneWard_Icon;
                pinData.m_save = false;
                pinData.m_checked = false;
                pinData.m_ownerID = 0L;
                _pins.Add(pinData, zdo);
            }

            foreach (KeyValuePair<Minimap.PinData, ZDO> p in _pins)
            {
                Minimap.instance.m_pins.Add(p.Key);
            }
        }
        private static void Prefix(Minimap __instance, Minimap.MapMode mode)
        {
            if (mode != Minimap.MapMode.Large)
            {
                foreach (KeyValuePair<Minimap.PinData, ZDO> pin in _pins) __instance.RemovePin(pin.Key);
                _pins.Clear();
            }

            if (mode != Minimap.MapMode.Large) return;
            CreatePins();
        }
    }
    [HarmonyPatch(typeof(Minimap), nameof(Minimap.OnMapLeftClick))]
    private static class PatchClickIconMinimap
    {
        private static bool Prefix()
        {
            Vector3 pos = Minimap.instance.ScreenToWorldPoint(Input.mousePosition);
            Minimap.PinData closestPin = Extensions.GetCustomPin(PINTYPEWARD, pos, Minimap.instance.m_removeRadius * (Minimap.instance.m_largeZoom * 2f));
            if (closestPin != null && _pins.TryGetValue(closestPin, out ZDO zdo))
            {
                ArcaneWardUI.Show(zdo);
                Minimap.instance.SetMapMode(Minimap.MapMode.Small);
                return false;
            }

            return true;
        }
    }
}