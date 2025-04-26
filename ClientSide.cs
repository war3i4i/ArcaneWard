using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Audio;

namespace kg_ArcaneWard;

public static class ClientSide
{
    private static readonly Dictionary<Minimap.PinData, ZDO> _pins = new();
    private static readonly List<Minimap.PinData> _radiusPins = new();
    private const Minimap.PinType PINTYPEWARD = (Minimap.PinType)181;
    private const Minimap.PinType PINTYPERADIUS = (Minimap.PinType)182;
    [HarmonyPatch(typeof(Game), nameof(Game.Start))] static class Game_Start_Patch { static void Postfix() => ArcaneWardComponent._canPlaceWard = false; }
    [HarmonyPatch(typeof(Player), nameof(Player.TryPlacePiece))]
    static class PlacePiece_Patch
    {
        static bool Prefix(Piece piece, ref bool __result)
        {
            if (piece.GetComponent<ArcaneWardComponent>() is not {} aw) return true;
            if (Player.m_debugMode) return true;
            if (!Player.m_localPlayer?.m_placementGhost) return true; 
            Vector3 pos = Player.m_localPlayer.m_placementGhost.transform.position;
            long playerID = Game.instance.m_playerProfile.m_playerID;
            IEnumerable<ArcaneWardComponent> nonPermittedWards = ArcaneWardComponent._instances.Where(x => !x.IsPermitted(playerID));
            foreach (var ward in nonPermittedWards)
            {
                if (ward.IsInside_X2(pos, 1f))
                { 
                    MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, "<color=red>$kg_arcanewardinside</color>");
                    __result = false;
                    return false;
                }
            }
            if (ZNet.instance.IsServer() || ArcaneWardComponent._canPlaceWard) return true;
            MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, "<color=red>$kg_arcanewardlimit</color>");
            __result = false;
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
            if (!Player.m_debugMode) AllWards.RemoveAll(zdo => !zdo.GetPermittedPlayersHashset().Contains(Player.m_localPlayer.GetPlayerID()));
            for (var i = 0; i < AllWards.Count; ++i)
            {
                var zdo = AllWards[i];
                if (!zdo.IsValid()) continue;
                string name = zdo.GetName();
                float fuel = zdo.GetFloat("Fuel");
                bool isActivated = zdo.GetBool("Enabled"); 
                int radius = zdo.GetInt("Radius"); 
                string colorName = isActivated && fuel > 0 ? "<color=green>" : "<color=red>";
                Minimap.PinData wardPin = new Minimap.PinData
                {
                    m_type = PINTYPEWARD, 
                    m_name = $"{colorName}{name}</color>",
                    m_pos = zdo.GetPosition(),
                    m_icon = ArcaneWard.ArcaneWard_Icon,
                    m_save = false,
                    m_checked = false,
                    m_ownerID = 0L
                };
                if (!string.IsNullOrEmpty(wardPin.m_name)) wardPin.m_NamePinData = new Minimap.PinNameData(wardPin);
                _pins.Add(wardPin, zdo);

                if (ArcaneWard.RadiusOnMap.Value)
                {
                    Minimap.PinData radiusPin = new Minimap.PinData
                    {
                        m_type = PINTYPERADIUS,
                        m_pos = zdo.GetPosition(),
                        m_name = "", 
                        m_icon = isActivated && fuel > 0 ? ArcaneWard.ArcaneWard_Radius_Icon : ArcaneWard.ArcaneWard_Radius_Icon_Disabled,
                        m_save = false,
                        m_checked = false,
                        m_ownerID = 0L,
                        m_worldSize = radius * 2f
                    };
                    _radiusPins.Add(radiusPin);
                }
            }
            
            foreach (Minimap.PinData pin in _radiusPins) Minimap.instance.m_pins.Add(pin);
            foreach (KeyValuePair<Minimap.PinData, ZDO> pin in _pins) Minimap.instance.m_pins.Add(pin.Key);
        }
        private static void Prefix(Minimap __instance, Minimap.MapMode mode)
        {
            if (mode != Minimap.MapMode.Large)
            {
                foreach (Minimap.PinData pin in _radiusPins) __instance.RemovePin(pin);
                foreach (KeyValuePair<Minimap.PinData, ZDO> pin in _pins) __instance.RemovePin(pin.Key);
                _radiusPins.Clear();
                _pins.Clear();
            }
            if (mode != Minimap.MapMode.Large) return;
            if (ArcaneWard.ShowIconsOnMap.Value) CreatePins(); 
        }
    }
     
    [HarmonyPatch(typeof(Minimap), nameof(Minimap.OnMapLeftClick))]
    private static class PatchClickIconMinimap
    {
        private static bool Prefix(bool __runOriginal)
        { 
            if (!__runOriginal) return false; 
            if (ArcaneWard.UseShiftLeftClick.Value && !Input.GetKey(KeyCode.LeftShift)) return true;
            Vector3 pos = Minimap.instance.ScreenToWorldPoint(Input.mousePosition);
            Minimap.PinData closestPin = Extensions.GetCustomPin(PINTYPEWARD, pos, Minimap.instance.m_removeRadius * (Minimap.instance.m_largeZoom * 2f), Extensions.PinVisibility.Visible);
            if (closestPin == null || !_pins.TryGetValue(closestPin, out ZDO zdo)) return true;
            ArcaneWardUI.Show(zdo);
            Minimap.instance.SetMapMode(Minimap.MapMode.Small);
            return false;
        }
    }
}