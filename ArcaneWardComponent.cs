using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using fastJSON;
using HarmonyLib;
using UnityEngine;

namespace kg_ArcaneWard;

[Flags]
public enum Protection : long
{
    None = 0,
    [Extensions.ProtectionIcon("ArmorStand")] Armor_Stand = 1,
    [Extensions.ProtectionIcon("piece_beehive")] Beehive = 2,
    [Extensions.ProtectionIcon("piece_chest_wood")] Container = 4,
    [Extensions.ProtectionIcon("wood_door")] Door = 8,
    [Extensions.ProtectionIcon("fermenter")] Fermenter = 16,
    [Extensions.ProtectionIcon("incinerator")] Incinerator = 32,
    [Extensions.ProtectionIcon("itemstand")] Item_Stand = 64,
    [Extensions.ProtectionIcon("piece_workbench")] Crafting_Stations = 128,
    [Extensions.ProtectionIcon("Hammer")] Build_Piece = 256,
    [Extensions.ProtectionIcon("Hammer")] Destroy_Piece = 512,
    [Extensions.ProtectionIcon("piece_sapcollector")] Sap_Collector = 1024,
    [Extensions.ProtectionIcon("sign")] Sign = 2048,
    [Extensions.ProtectionIcon("portal_stone")] Portal = 4096,
    [Extensions.ProtectionIcon("piece_trap_troll")] Trap = 8192,
    [Extensions.ProtectionIcon("Ruby")] Pickup_Item = 16384,
    [Extensions.ProtectionIcon("Ruby")] Drop_Item = 32768,
    [Extensions.ProtectionIcon("VikingShip")] Ship = 65536,
    [Extensions.ProtectionIcon("GreydwarfEye")] Push_Players = 131072,
    [Extensions.ProtectionIcon("SledgeDemolisher")] No_Build_Damage = 262144,
    [Extensions.ProtectionIcon("Carrot")] Pickables = 524288,
    [Extensions.ProtectionIcon("piece_cartographytable")] Map_Table = 1048576,
}

public class ArcaneWardComponent : MonoBehaviour, Interactable, Hoverable
{
    public static bool _canPlaceWard;
    public static readonly long FullProtection = Enum.GetValues(typeof(Protection)).Cast<long>().Sum();
    public static readonly Protection[] ProtectionValues = Enum.GetValues(typeof(Protection)).Cast<Protection>().Where(x => x != Protection.None).ToArray();

    public static readonly List<ArcaneWardComponent> _instances = [];
    private static readonly int RefractionIntensity = Shader.PropertyToID("_RefractionIntensity");
    private List<Material> _wardMaterials;
    public ZNetView _znet;
    public Piece _piece;
    private GameObject _vfx;
    private GameObject _bubble; 
    private Animator _animator;
    private EffectArea _effectArea;
    private CircleProjector _areaMarker;

    public static bool CheckFlag(Vector3 point, bool skipPermitted, Protection flag, bool flash = true)
    {
        if (ArcaneWard.DisabledProtection.Value.HasFlagFast(flag)) return false;
        long id = Game.instance.m_playerProfile.m_playerID;
        IEnumerable<ArcaneWardComponent> wards = _instances.Where(x => x.IsEnabled && x.IsInside(point));
        foreach (var ward in wards)
        {
            if (!ward.Protection.HasFlagFast(flag)) continue;
            if (skipPermitted && ward.IsPermitted(id)) continue;
            if (flash) ward.Flash();
            return true;
        }
        return false;
    }

    public static readonly int _cache_Key_Name = "Name".GetStableHashCode();
    public static readonly int _cache_Key_Enabled = "Enabled".GetStableHashCode();
    public static readonly int _cache_Key_Bubble = "Bubble".GetStableHashCode();
    public static readonly int _cache_Key_BubbleFraction = "BubbleFraction".GetStableHashCode();
    public static readonly int _cache_Key_Notify = "Notify".GetStableHashCode();
    public static readonly int _cache_Key_Radius = "Radius".GetStableHashCode();
    public static readonly int _cache_Key_Protection = "Protection".GetStableHashCode();
    public static readonly int _cache_Key_LastNotifyTime = "LastNotifyTime".GetStableHashCode();
    public static readonly int _cache_Key_Fuel = "Fuel".GetStableHashCode();
    public static readonly int _cache_Key_LastUpdateTime = "LastUpdateTime".GetStableHashCode();
    public string Name
    {
        get => _znet.m_zdo.GetString(_cache_Key_Name, "$kg_arcaneward");
        set => _znet.m_zdo.Set(_cache_Key_Name, value);
    }
    public bool IsEnabled => IsActivated && Fuel > 0;
    public bool IsActivated
    { 
        get => _znet.m_zdo.GetBool(_cache_Key_Enabled);
        set => _znet.m_zdo.Set(_cache_Key_Enabled, value);
    }
    public bool IsBubbleEnabled
    {
        get => _znet.m_zdo.GetBool(_cache_Key_Bubble, true);
        set => _znet.m_zdo.Set(_cache_Key_Bubble, value);
    }
    public int BubbleFraction
    {
        get => _znet.m_zdo.GetInt(_cache_Key_BubbleFraction, 1);
        set => _znet.m_zdo.Set(_cache_Key_BubbleFraction, value);
    }
    public bool IsNotifyEnabled
    {
        get => _znet.m_zdo.GetBool(_cache_Key_Notify);
        set => _znet.m_zdo.Set(_cache_Key_Notify, value);
    }
    private float Fuel
    { 
        get => _znet.m_zdo.GetFloat(_cache_Key_Fuel);
        set => _znet.m_zdo.Set(_cache_Key_Fuel, value);
    }
    private int LastUpdateTime
    { 
        get => _znet.m_zdo.GetInt(_cache_Key_LastUpdateTime);
        set => _znet.m_zdo.Set(_cache_Key_LastUpdateTime, value);
    }
    public int Radius
    {
        get => Mathf.Clamp(_znet.m_zdo.GetInt(_cache_Key_Radius, ArcaneWard.WardDefaultRadius.Value), ArcaneWard.WardMinRadius.Value, ArcaneWard.WardMaxRadius.Value);
        set => _znet.m_zdo.Set(_cache_Key_Radius, value);
    }
    private Protection Protection
    {
        get => (Protection)_znet.m_zdo.GetLong(_cache_Key_Protection, FullProtection);
        set => _znet.m_zdo.Set(_cache_Key_Protection, (long)value);
    }
    private int LastNotifyTime
    {
        get => _znet.m_zdo.GetInt(_cache_Key_LastNotifyTime);
        set => _znet.m_zdo.Set(_cache_Key_LastNotifyTime, value);
    }
    private void OnDestroy() => _instances.Remove(this);
    private string CreatorName => _znet.m_zdo.GetString(ZDOVars.s_creatorName);
    public bool IsInside(Vector3 point, float margin = 0f) => Vector3.Distance(point, transform.position) <= Radius + margin;
    private Dictionary<long, string> _cachedPermittedPlayers = [];
    public bool IsPermitted(long playerID) => _cachedPermittedPlayers.ContainsKey(playerID);

    private void Awake()
    { 
        _znet = GetComponent<ZNetView>();
        if (!_znet.IsValid()) return;
        _instances.Add(this);
        _areaMarker = transform.Find("AreaMarket_Main").GetComponent<CircleProjector>();
        _piece = GetComponent<Piece>();
        _bubble = transform.Find("Bubble").gameObject;
        _vfx = transform.Find("VFX").gameObject;
        _vfx.SetActive(IsEnabled);
        _animator = transform.Find("Ward").GetComponent<Animator>();
        _animator.enabled = false;
        _effectArea = transform.Find("PlayerBase").GetComponent<EffectArea>();
        _effectArea.transform.localScale = new Vector3(Radius * 2, Radius * 2, Radius * 2);
        _wardMaterials = transform.GetChild(3).GetComponentsInChildren<MeshRenderer>(true).Select(x => x.material).ToList();
        _cachedPermittedPlayers = _znet.m_zdo.GetPermittedPlayers();
        _areaMarker.gameObject.SetActive(false);
        _bubble.GetComponent<MeshRenderer>().material.SetFloat(RefractionIntensity, BubbleFraction * 0.005f);
        _znet.Register<string>("RPC_ResetCache", ResetCache);
        _znet.Register<bool>("RPC_EnableSwitch", EnabledSwitch);
        _znet.Register<int>("RPC_AddFuel", AddFuel);
        _znet.Register<ZPackage>("RPC_UpdateData", UpdateData);
        if (_znet.IsOwner() && CreatorName == "")
        {  
            Setup(Game.instance.GetPlayerProfile().GetName(),
                ZNet.m_onlineBackend == OnlineBackendType.Steamworks
                    ? PrivilegeManager.GetNetworkUserId().Split('_')[1] 
                    : PrivilegeManager.GetNetworkUserId()); 
        }
        InvokeRepeating(nameof(UpdateStatus), 1f, 1);
    }

    private void UpdateData(long sender, ZPackage pkg)
    {
        if (!_znet.IsOwner()) return;
        string wardName = pkg.ReadString();
        bool wardEnabled = pkg.ReadBool();
        bool bubble = pkg.ReadBool();
        bool notify = pkg.ReadBool();
        int radius = pkg.ReadInt();
        int fraction = pkg.ReadInt();
        Protection protection = (Protection)pkg.ReadLong();
        int permittedCount = pkg.ReadInt();
        Dictionary<long, string> permittedPlayers = new();
        for (int i = 0; i < permittedCount; i++)
        {
            long id = pkg.ReadLong();
            string playerName = pkg.ReadString();
            permittedPlayers[id] = playerName;
        }
        Name = wardName;
        EnabledSwitch(0, !wardEnabled);
        IsBubbleEnabled = bubble;
        IsNotifyEnabled = notify;
        Radius = radius;
        BubbleFraction = fraction;
        Protection = protection;
        _znet.m_zdo.SetPermittedPlayers(permittedPlayers);
        _znet.InvokeRPC(ZNetView.Everybody, "RPC_ResetCache", [JSON.ToJSON(permittedPlayers)]);
    }
    
    private void AddFuel(long sender, int fuel)
    {
        if (!_znet.IsOwner()) return;
        Instantiate(ArcaneWard.FlashShield_Fuel, transform.position, Quaternion.identity);
        Fuel += fuel;
        if (Fuel > ArcaneWard.WardMaxFuel.Value) Fuel = ArcaneWard.WardMaxFuel.Value;
    }

    private void EnabledSwitch(long obj, bool prevState)
    {
        bool currentState = IsActivated;
        if (currentState == !prevState) return;
        IsActivated = !prevState;
        Instantiate(prevState ? ArcaneWard.FlashShield_Deactivate : ArcaneWard.FlashShield_Activate, transform.position, Quaternion.identity);
        LastUpdateTime = (int)EnvMan.instance.m_totalSeconds;
    }
    private void ResetCache(long obj, string permittedJSON)
    {
        _cachedPermittedPlayers = JSON.ToObject<Dictionary<long, string>>(permittedJSON);
        _cachedRadius = -1;
        _cachedFraction = -1;
    }

    private void Setup(string creatorName, string id)
    {
        LastUpdateTime = (int)EnvMan.instance.m_totalSeconds;
        _canPlaceWard = false;
        _znet.m_zdo.Set(ZDOVars.s_creatorName, creatorName);
        _znet.m_zdo.Set("ArcaneWard_ID", id);
        Dictionary<long, string> owner = new() { { Game.instance.m_playerProfile.m_playerID, creatorName + " <color=green>[$kg_arcaneward_owner]</color>" } };
        _znet.m_zdo.SetPermittedPlayers(owner);
        _znet.InvokeRPC(ZNetView.Everybody, "RPC_ResetCache", [JSON.ToJSON(owner)]);
        ZRoutedRpc.instance.InvokeRoutedRPC("ArcaneWard Placed", [null]);
    }

    public string GetHoverText()
    { 
        if (!_znet.IsValid() || !Player.m_localPlayer) return "";
        if (!IsPermitted(Game.instance.m_playerProfile.m_playerID) && !Player.m_debugMode)
        { 
            return "$kg_cantviewarcaneward".Localize();  
        }

        StringBuilder stringBuilder = new StringBuilder(256); 
        stringBuilder.Append($"Charge: <color=green>{((int)Fuel).ToTime()}</color> | <color=yellow>{ArcaneWard.WardMaxFuel.Value.ToTimeNoS()}</color>\n");
        var currenStatus = IsActivated ? "<color=green>$kg_arcaneward_activated</color>" : "<color=#FF0000>$kg_arcaneward_deactivated</color>";
        if (Fuel <= 0) currenStatus += " <color=red>($kg_arcaneward_nofuel)</color>";
        stringBuilder.Append(_piece.m_name + $" ( {currenStatus} )");
        Dictionary<long, string> permittedPlayers = _cachedPermittedPlayers;
        stringBuilder.Append("\n$piece_guardstone_additional: ");
        stringBuilder.Append(permittedPlayers.Count == 0 ? "$piece_guardstone_noone" : string.Join(", ", permittedPlayers.Values));
        stringBuilder.Append(IsActivated ? "\n [<color=yellow><b>$KEY_Use</b></color>] $kg_arcaneward_deactivate" : "\n [<color=yellow><b>$KEY_Use</b></color>] $kg_arcaneward_activate");
        stringBuilder.Append("\n [<color=yellow><b>L.Shift + $KEY_Use</b></color>] $kg_arcaneward_openui");
        return Localization.instance.Localize(stringBuilder.ToString());
    }  
    
    private int LastFlashTime;
    public void Flash() 
    {
        if (EnvMan.instance.m_totalSeconds - LastFlashTime <= 2f) return;
         
        if (IsNotifyEnabled && !IsPermitted(Game.instance.m_playerProfile.m_playerID))
        {
            int now = (int)EnvMan.instance.m_totalSeconds; 
            if (now - LastNotifyTime >= 8)
            {
                LastNotifyTime = now;
                IEnumerable<ZNet.PlayerInfo> list = ZNet.instance.GetPlayerList().Where(x => ZDOMan.instance.GetZDO(x.m_characterID) != null && _cachedPermittedPlayers.ContainsKey(ZDOMan.instance.GetZDO(x.m_characterID).GetLong(ZDOVars.s_playerID)));
                string wardName = Name;
                foreach (var playerInfo in list)
                {
                    ZRoutedRpc.instance.InvokeRoutedRPC(playerInfo.m_characterID.UserID, "ArcaneWard Notify", [wardName]);
                }
            } 
        }
        
        LastFlashTime = (int)EnvMan.instance.m_totalSeconds;
        if (!ArcaneWard.WardFlash.Value) return;
        var go = Instantiate(ArcaneWard.FlashShield, transform.position, Quaternion.identity);
        float rad = Radius / 32f;
        go.transform.Find("Dome").localScale = new Vector3(rad, rad, rad);
    }

    private int _cachedRadius;
    private int _cachedFraction;
    private void UpdateStatus()
    { 
        bool _enabled = IsEnabled;
        if (_enabled)
        {
            _wardMaterials.ForEach(m => m.EnableKeyword("_EMISSION")); 
            _vfx.SetActive(true);
            _bubble.SetActive(IsBubbleEnabled);
            _animator.enabled = true;
        }
        else
        {
            _wardMaterials.ForEach(m => m.DisableKeyword("_EMISSION"));
            _vfx.SetActive(false);
            _bubble.SetActive(false);
            _animator.enabled = false;
        }
        
        if (Player.m_localPlayer && IsInside(Player.m_localPlayer.transform.position)) _areaMarker.gameObject.SetActive(true);
        else _areaMarker.gameObject.SetActive(false);

        int r = Radius;
        if (r != _cachedRadius)
        {
            _areaMarker.m_radius = r;
            _effectArea.transform.localScale = new Vector3(r * 2, r * 2, r * 2);
            _bubble.transform.localScale = new Vector3(r * 2, r * 2, r  * 2);
            _cachedRadius = r;
        }
        int f = BubbleFraction;
        if (f != _cachedFraction)
        {
            _bubble.GetComponent<MeshRenderer>().material.SetFloat(RefractionIntensity, f * 0.005f);
        }
        if (!_znet.HasOwner() || !_znet.IsOwner()) return;

        int currentTime = (int)EnvMan.instance.m_totalSeconds; 
        int deltaTime = currentTime - LastUpdateTime;
        if (_enabled)
        {  
            Fuel -= deltaTime;
            if (Fuel < 0)
            {
                Fuel = 0;
                _znet.InvokeRPC("RPC_EnableSwitch", true);
            }
        }
        LastUpdateTime = (int)EnvMan.instance.m_totalSeconds;
    }

    public string GetHoverName() => "$kg_arcaneward";
    
    public bool Interact(Humanoid user, bool hold, bool alt)
    {
        Player player = user as Player; 
        if (!IsPermitted(player!.GetPlayerID()) && !Player.m_debugMode) return false;
        if (Input.GetKey(KeyCode.LeftShift))
        {
            ArcaneWardUI.Show(_znet.m_zdo);
            return true;
        }
        _znet.InvokeRPC("RPC_EnableSwitch", IsActivated);
        return true;
    }

    public bool UseItem(Humanoid user, ItemDrop.ItemData item)
    {
        return false;
    }

    private void FixedUpdate()
    {
        if (!_znet.IsValid() || !Player.m_localPlayer) return;
        if (!IsInside(Player.m_localPlayer.transform.position, margin: 0.5f)) return;
        if (!IsEnabled || IsPermitted(Game.instance.m_playerProfile.m_playerID)) return;
        if (!Protection.HasFlagFast(Protection.Push_Players)) return;
        if (ArcaneWard.DisabledProtection.Value.HasFlagFast(Protection.Push_Players)) return;
        Player p = Player.m_localPlayer;
        float pushValue = Time.fixedDeltaTime * 7f;
        Vector3 newVector3 = p.transform.position + (p.transform.position - transform.position).normalized * pushValue;
        p.m_body.isKinematic = true;
        p.transform.position = new Vector3(newVector3.x, p.transform.position.y, newVector3.z);
        p.m_body.isKinematic = false;
    }
}

[SuppressMessage("ReSharper", "UnusedMember.Local")]
[SuppressMessage("ReSharper", "UnusedType.Local")]
public static class WardProtectionPatches
{
    [HarmonyPatch(typeof(ArmorStand),nameof(ArmorStand.UseItem))]
    private static class ArmorStand_UseItem_Patch
    {
        private static bool Prefix(ArmorStand __instance) => !ArcaneWardComponent.CheckFlag(__instance.transform.position, true, Protection.Armor_Stand);
    }
    [HarmonyPatch(typeof(Beehive),nameof(Beehive.Interact))]
    private static class Beehive_Interact_Patch
    {
        private static bool Prefix(Beehive __instance) => !ArcaneWardComponent.CheckFlag(__instance.transform.position, true, Protection.Beehive);
    }
    [HarmonyPatch(typeof(Container),nameof(Container.Interact))]
    private static class Container_Interact_Patch
    {
        private static bool Prefix(Container __instance) => !ArcaneWardComponent.CheckFlag(__instance.transform.position, true, Protection.Container);
    }
    [HarmonyPatch(typeof(Door),nameof(Door.Interact))]
    private static class Door_Interact_Patch
    {
        private static bool Prefix(Door __instance) => !ArcaneWardComponent.CheckFlag(__instance.transform.position, true, Protection.Door);
    }
    [HarmonyPatch(typeof(Fermenter),nameof(Fermenter.Interact))]
    private static class Fermenter_Interact_Patch
    {
        private static bool Prefix(Fermenter __instance) => !ArcaneWardComponent.CheckFlag(__instance.transform.position, true, Protection.Fermenter);
    }
    [HarmonyPatch(typeof(Incinerator),nameof(Incinerator.OnIncinerate))]
    private static class Incinerator_OnIncinerate_Patch
    {
        private static bool Prefix(Incinerator __instance) => !ArcaneWardComponent.CheckFlag(__instance.transform.position, true, Protection.Incinerator);
    }
    [HarmonyPatch(typeof(ItemStand),nameof(ItemStand.Interact))]
    private static class ItemStand_Interact_Patch
    {
        private static bool Prefix(ItemStand __instance) => !ArcaneWardComponent.CheckFlag(__instance.transform.position, true, Protection.Item_Stand);
    }
    [HarmonyPatch]
    private static class MapTable_OnRead_Patch
    {
        private static IEnumerable<MethodInfo> TargetMethods()
        {
            yield return AccessTools.Method(typeof(MapTable), nameof(MapTable.OnRead), [typeof(Switch), typeof(Humanoid), typeof(ItemDrop.ItemData), typeof(bool)]);
            yield return AccessTools.Method(typeof(MapTable), nameof(MapTable.OnWrite));
        }
        private static bool Prefix(MapTable __instance) => !ArcaneWardComponent.CheckFlag(__instance.transform.position, true, Protection.Map_Table);
    }
    [HarmonyPatch(typeof(Player),nameof(Player.PlacePiece))]
    private static class Player_PlacePiece_Patch
    {
        private static bool Prefix(Vector3 pos) => !ArcaneWardComponent.CheckFlag(pos, true, Protection.Build_Piece);
    }
    [HarmonyPatch(typeof(Player),nameof(Player.CheckCanRemovePiece))]
    private static class Player_RemovePiece_Patch
    {
        private static bool Prefix(Piece piece) => !ArcaneWardComponent.CheckFlag(piece.transform.position, true, Protection.Destroy_Piece);
    }
    [HarmonyPatch(typeof(SapCollector),nameof(SapCollector.Interact))]
    private static class SapCollector_Interact_Patch
    {
        private static bool Prefix(SapCollector __instance) => !ArcaneWardComponent.CheckFlag(__instance.transform.position, true, Protection.Sap_Collector);
    }
    [HarmonyPatch(typeof(Sign),nameof(Sign.Interact))]
    private static class Sign_Interact_Patch
    {
        private static bool Prefix(Sign __instance) => !ArcaneWardComponent.CheckFlag(__instance.transform.position, true, Protection.Sign);
    }
    private static class TeleportWorld_Interact_Patch
    {
        private static IEnumerable<MethodInfo> TargetMethods()
        {
            yield return AccessTools.Method(typeof(TeleportWorld), nameof(TeleportWorld.Interact));
            yield return AccessTools.Method(typeof(TeleportWorld), nameof(TeleportWorld.Teleport));
        }
        private static bool Prefix(TeleportWorld __instance) => !ArcaneWardComponent.CheckFlag(__instance.transform.position, true, Protection.Portal);
    }
    [HarmonyPatch(typeof(Trap),nameof(Trap.Interact))]
    private static class Trap_Interact_Patch
    {
        private static bool Prefix(Trap __instance) => !ArcaneWardComponent.CheckFlag(__instance.transform.position, true, Protection.Trap);
    }
    [HarmonyPatch(typeof(ItemDrop),nameof(ItemDrop.Pickup))]
    private static class ItemDrop_Pickup_Patch
    {
        private static bool Prefix(ItemDrop __instance) => !ArcaneWardComponent.CheckFlag(__instance.transform.position, true, Protection.Pickup_Item);
    } 
    [HarmonyPatch(typeof(Humanoid),nameof(Humanoid.DropItem))]
    private static class Player_DropItem_Patch
    {
        private static bool Prefix(Player __instance) => !ArcaneWardComponent.CheckFlag(__instance.transform.position, true, Protection.Drop_Item);
    }
    [HarmonyPatch(typeof(ShipControlls),nameof(ShipControlls.Interact))]
    private static class ShipControlls_Interact_Patch
    {
        private static bool Prefix(ShipControlls __instance) => !ArcaneWardComponent.CheckFlag(__instance.transform.position, true, Protection.Ship);
    }
    [HarmonyPatch(typeof(WearNTear),nameof(WearNTear.Damage))]
    private static class WearNTear_Damage_Patch
    {
        private static bool Prefix(WearNTear __instance) => !ArcaneWardComponent.CheckFlag(__instance.transform.position, false, Protection.No_Build_Damage);
    }
    [HarmonyPatch(typeof(Pickable),nameof(Pickable.Interact))]
    private static class Pickable_Interact_Patch
    {
        private static bool Prefix(Pickable __instance) => !ArcaneWardComponent.CheckFlag(__instance.transform.position, true, Protection.Pickables);
    }
    [HarmonyPatch(typeof(Piece),nameof(Piece.CanBeRemoved))]
    private static class Piece__Patch 
    {
        private static void Postfix(Piece __instance, ref bool __result)
        {
            if (__instance.GetComponent<ArcaneWardComponent>() is not {} comp) return;
            if (!comp.IsPermitted(Game.instance.m_playerProfile.m_playerID) && !Player.m_debugMode)
            {
                __result = false;
            }
        }
    }
    [HarmonyPatch(typeof(ShieldGenerator),nameof(ShieldGenerator.CheckProjectile))]
    private static class ShieldGenerator_CheckProjectile_Patch
    {
        private static void Postfix(ref Projectile projectile)
        {
            if (!ArcaneWard.WardBlockProjectiles.Value || !projectile) return;
            foreach (var ward in ArcaneWardComponent._instances)
            {
                if (!ward.IsEnabled || !ward.IsBubbleEnabled) continue;
                Vector3 center = ward.transform.position;
                Vector3 start = projectile.m_startPoint;
                Vector3 current = projectile.transform.position; 
                if (Vector3.Distance(center, start) > ward.Radius && Vector3.Distance(center, current) <= ward.Radius)
                {
                    projectile.OnHit(null, current, false, -center);
                    ZNetScene.instance.Destroy(projectile.gameObject);
                    return;
                }
            }
        }
    }
}