using System.Collections;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using fastJSON;
using HarmonyLib;
using UnityEngine;

namespace kg_ArcaneWard;

public static class ServerSide
{
    public class WardManager
    {
        private enum PlayerStatus
        { 
            VIP,
            User
        }

        private PlayerStatus GetPlayerStatus(string id) => ZNet.instance.ListContainsId(VIPplayersList, id) ? PlayerStatus.VIP : PlayerStatus.User;
        private readonly string _path;
        public readonly Dictionary<string, int> PlayersWardData = new();

        public WardManager(string path)
        {
            _path = path;
            if (!File.Exists(_path))
            {
                File.Create(_path).Dispose();
            }
            else
            {
                string data = File.ReadAllText(_path);
                if (!string.IsNullOrEmpty(data))
                    PlayersWardData = JSON.ToObject<Dictionary<string, int>>(data);
            }
        }

        public bool CanBuildWard(string id)
        {
            if (!PlayersWardData.ContainsKey(id)) return true;
            return GetPlayerStatus(id) switch
            {
                PlayerStatus.VIP => PlayersWardData[id] < MaxAmountOfWards_VIP.Value,
                PlayerStatus.User => PlayersWardData[id] < MaxAmountOfWards.Value,
                _ => false
            };
        }

        public void Save()
        {
            File.WriteAllText(_path, JSON.ToNiceJSON(PlayersWardData));
        }
    }

    [HarmonyPatch(typeof(ZNet),nameof(ZNet.Awake))]
    private static class ZNet_Awake_Patch 
    {
        private static void Prefix(ZNet __instance)
        {
            string folder = Path.Combine(Paths.ConfigPath, "ArcaneWard"); 
            FileHelpers.FileLocation loc = new FileHelpers.FileLocation(FileHelpers.FileSource.Local, Path.Combine(folder, "VIPplayers.txt"));
            VIPplayersList = new SyncedList(loc, "");
        }
    } 
    
    private static SyncedList VIPplayersList;
    private static WardManager _wardManager;
    private static ConfigEntry<int> MaxAmountOfWards;
    private static ConfigEntry<int> MaxAmountOfWards_VIP;
    private static FileSystemWatcher fsw;

    public static void ServerSideInit()
    {
        string folder = Path.Combine(Paths.ConfigPath, "ArcaneWard"); 
        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
        _wardManager = new WardManager(Path.Combine(folder, "WardData.json"));
  
        MaxAmountOfWards = ArcaneWard._thistype.Config.Bind("Limitations", "MaxAmountOfWards", 10, "Max amount of wards");
        MaxAmountOfWards_VIP = ArcaneWard._thistype.Config.Bind("Limitations", "MaxAmountOfWards_VIP", 30, "Max amount of wards for VIP");

        fsw = new FileSystemWatcher(Paths.ConfigPath)
        {
            Filter = Path.GetFileName(ArcaneWard._thistype.Config.ConfigFilePath),
            EnableRaisingEvents = true,
            IncludeSubdirectories = true,
            SynchronizingObject = ThreadingHelper.SynchronizingObject
        };
        fsw.Changed += ConfigChanged;
    }

    private static void ConfigChanged(object sender, FileSystemEventArgs e)
    {
        ArcaneWard._thistype.StartCoroutine(DelayReloadConfigFile(ArcaneWard._thistype.Config));
    }

    private static IEnumerator DelayReloadConfigFile(ConfigFile file)
    {
        yield return new WaitForSecondsRealtime(2.5f);
        file.Reload();
    }

    [HarmonyPatch(typeof(ZDOMan), nameof(ZDOMan.HandleDestroyedZDO))]
    static class ZDOMan_Patch
    {
        private static readonly int ArcaneWard_ID = "ArcaneWard_ID".GetStableHashCode();
        private static readonly int Ward = "ArcaneWard".GetStableHashCode();
        static void Prefix(ZDOMan __instance, ZDOID uid)
        {
            if (!ZNet.instance.IsServer()) return;
            ZDO zdo = __instance.GetZDO(uid);
            if (zdo == null) return;
            if (zdo.m_prefab == Ward)
            {
                string id = zdo.GetString(ArcaneWard_ID);
                if (!_wardManager.PlayersWardData.ContainsKey(id)) return;
                _wardManager.PlayersWardData[id]--;
                if (_wardManager.PlayersWardData[id] < 0) _wardManager.PlayersWardData[id] = 0;
                _wardManager.Save();
                ZNetPeer peer = ZNet.instance.GetPeerByHostName(id);
                if (peer != null) ZRoutedRpc.instance.InvokeRoutedRPC(peer.m_uid, "ArcaneWard Data", _wardManager.CanBuildWard(id));
            }
         
        }
    }

    [HarmonyPatch(typeof(ZNet), "RPC_PeerInfo")]
    private static class ZnetSync
    {
        private static void Postfix(ZRpc rpc)
        {
            if (!ZNet.instance.IsServer()) return;
            ZNetPeer peer = ZNet.instance.GetPeer(rpc);
            string id = peer.m_socket.GetHostName();
            ZRoutedRpc.instance.InvokeRoutedRPC(peer.m_uid, "ArcaneWard Data", _wardManager.CanBuildWard(id));
        }
    }

    [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
    private static class ZNetScene_Awake_Patch
    {
        private static void Postfix()
        {
            ZRoutedRpc.instance.Register("ArcaneWard Placed", WardPlaced);

            if (ZNet.instance.IsServer())
            {
                ZNetScene.instance.StartCoroutine(SendWardsToClients());
            }
        }

        private static void WardPlaced(long sender)
        {
            ZNetPeer peer = ZNet.instance.GetPeer(sender);
            if (peer == null) return;
            var id = peer.m_socket.GetHostName();
            if (_wardManager.PlayersWardData.ContainsKey(id))
            {
                _wardManager.PlayersWardData[id]++;
            }
            else
            {
                _wardManager.PlayersWardData[id] = 1;
            }

            _wardManager.Save();
            ZRoutedRpc.instance.InvokeRoutedRPC(peer.m_uid, "ArcaneWard Data", _wardManager.CanBuildWard(id));
        }
    }

    private static readonly List<ZDO> TempWardsList = [];
    public const string toSearch = "ArcaneWard";
    
    private static IEnumerator SendWardsToClients()
    {
        for (;;)
        {
            TempWardsList.Clear();
            int index = 0;
            while (!ZDOMan.instance.GetAllZDOsWithPrefabIterative(toSearch, TempWardsList, ref index)) { yield return null; }
            for (var i = 0; i < TempWardsList.Count; ++i)
            {
                ZDOMan.instance.ForceSendZDO(TempWardsList[i].m_uid);
            }

            yield return new WaitForSeconds(10f);
        }
    }
}