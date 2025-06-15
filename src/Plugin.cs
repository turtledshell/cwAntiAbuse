using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using BepInEx;
using ExitGames.Client.Photon.StructWrapping;
using HarmonyLib;
using MonoMod.Utils;
using Photon.Pun;
using UnityEngine;
using UnityEngine.UIElements;

namespace cwAnticheat {

    [BepInPlugin("org.turtledshell.cwAnticheat", "cwAnticheat", "1.0.0")]
    public class Plugin : BaseUnityPlugin {
        public static bool verbose = true;
        public static List<ACPlayer> players = new List<ACPlayer>();
        public static List<int[]> bills=[];
        public static bool setup=false;
        public static string[] whitelistedPrefabs = ["Player", "PlayerData", "ItemDataSyncer", "PickupHolder", "Projector", "PoolBall", "PoolRing", "DeckChair", "PodcastChair"];
        Harmony harmony = new Harmony(PluginInfo.PLUGIN_GUID);
        public static bool IsHost { get => MainMenuHandler.SteamLobbyHandler.IsMasterClient; }
        private void Awake() {
            harmony.PatchAll(typeof(PhotonViewPatch));
            harmony.PatchAll(typeof(SurfaceNetworkHandlerPatch));
            harmony.PatchAll(typeof(PlayerInventoryPatch));
            harmony.PatchAll(typeof(PickupPatch));
            harmony.PatchAll(typeof(PlayerPatch));
            harmony.PatchAll(typeof(MainMenuHandler));

            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
        }
        private void Update() {
            if (!setup) {
                harmony.PatchAll(typeof(PhotonNetworkPatch));
                setup = true;
            }
        }
        public static void Log(string s) {
            if (verbose) { Trace.WriteLine(s); }
        }
        public static ACPlayer GetPlayer(Player instance) {
            for (int i = players.Count - 1; i >= 0; i--) {
                if (players[i] == null) {
                    players.RemoveAt(i);
                } else if (players[i].ActorNumber == instance.refs.view.Owner.ActorNumber) {
                    players[i].player = instance;
                    return players[i];
                }
            }
            if (verbose) { Trace.TraceError("Player (" + instance.refs.view.Owner.NickName + ") was not found in players list! Adding it now."); }
            ACPlayer p = new ACPlayer(instance);
            players.Add(p);
            return p;
        }
        public static ACPlayer GetFromInventoryView(PhotonView view) {
            FieldInfo field = ReflectionHelper.Field.GetField<PlayerInventory>("m_photonView");
            foreach (ACPlayer player in players) {
                PlayerInventory playerInventory = null; player.player.TryGetInventory(out playerInventory);
                if (playerInventory == null) { return null; }
                if ((PhotonView)field.GetValue(playerInventory) == view) {
                    return player;
                }
            }
            return null;
        }
        public static float GetDistance(Vector3 pos1, Vector3 pos2) {
            float[] position3 = { pos1.x - pos2.x, pos1.y - pos2.y, pos1.z - pos2.z };
            Vector3 position = Vector3.zero;
            position.x = position3[0];
            position.y = position3[1];
            position.z = position3[2];
            float distance = (float)Math.Sqrt((position.x * position.x) + (position.y * position.y));
            distance = (float)Math.Sqrt((position.z * position.z) + (distance * distance));
            return distance;
    }
    }
    [HarmonyPatch(typeof(MainMenuHandler))]
    public class MainMenuPatch {
        [HarmonyPatch("LoadGameScene")]
        [HarmonyPrefix]
        public static void GameScenePatch() {
            Plugin.bills.Clear();
            Plugin.players.Clear();
        }
    }
    [HarmonyPatch(typeof(PhotonView))]
    public class PhotonViewPatch {

        [HarmonyPatch("RPC", new[] { typeof(string), typeof(RpcTarget), typeof(object[]) })]
        [HarmonyPrefix]
        public static void RPCPatch(PhotonView __instance, string methodName, RpcTarget target, params object[] parameters) {
            if (Plugin.IsHost && methodName == "RPCA_HospitalBill") {
                Plugin.bills.Add([(int)parameters[0], (int)parameters[1]]);
            }
            if (!Plugin.verbose) { return; }
            Plugin.Log("SendRPC" + ", Name: " + methodName + ", Target: " + target + ", Parameters: [" + string.Join(", ", parameters) + "]");
        }
    }
    [HarmonyPatch(typeof(PhotonNetwork))]
    public class PhotonNetworkPatch {
        [HarmonyPatch("NetworkInstantiate", new Type[] {typeof(InstantiateParameters), typeof(bool), typeof(bool)})]
        [HarmonyPrefix]
         public static bool InstantiatePatch(ref InstantiateParameters parameters, bool roomObject, bool instantiateEvent) { // i have NO idea whats going on with referencing this but it breaks stuff if i get any values from parameters
            if (!Plugin.IsHost) { return true;  }
            if (parameters.creator.ActorNumber != 1) {
                if (!Plugin.whitelistedPrefabs.Contains(parameters.prefabName)) {
                    Plugin.Log(parameters.creator.ActorNumber + " tried to instantiate \"" + parameters.prefabName + "\"");
                    return false;
                }
            }
            Plugin.Log("Creator: "+parameters.creator.ActorNumber+", Data: \""+parameters.prefabName+"\", roomObject: "+roomObject+", instantiateEvent: "+instantiateEvent);
            return true;
         }
    }
    [HarmonyPatch(typeof(SurfaceNetworkHandler))]
    public class SurfaceNetworkHandlerPatch {
        [HarmonyPatch("RPCA_HospitalBill")]
        [HarmonyPrefix]
        public static bool InstantiatePatch(int actorNumber, int moneyToRemove) {
            if (!Plugin.IsHost) { return true; }
            for (int i = 0; i < Plugin.bills.Count; i++) {
                if (Plugin.bills[i][0] == actorNumber && Plugin.bills[i][1] == moneyToRemove) {
                    Plugin.bills.RemoveAt(i);
                    return true;
                }
            }
            Trace.WriteLine("Actor "+actorNumber+" tried to remove "+moneyToRemove+" in a hospital bill.");
            return false;
        }
    }
    [HarmonyPatch(typeof(Pickup))]
    public class PickupPatch {
        [HarmonyPatch("RPC_RequestPickup")]
        [HarmonyPrefix]
        public static bool OnRequestPickup(Pickup __instance, int photonView) {
            if (!Plugin.IsHost) { return true; }
            Player component = PhotonNetwork.GetPhotonView(photonView).GetComponent<Player>();
            ACPlayer player = Plugin.GetPlayer(component);
            if (Plugin.GetDistance(component.HeadPosition(), __instance.itemInstance.transform.position) > 4.2) {
                Trace.WriteLine("Player: "+player.GetName()+" tried to grab an item with distance "+Vector3.Distance(component.HeadPosition(), __instance.transform.position));
                return false;
            }
            if (player.player == Player.localPlayer) {
                return true;
            }
            player.cachedPickup.AddFirst(new Tuple<byte, byte[]>(__instance.itemInstance.item.id, __instance.itemInstance.instanceData.Serialize(false)));
            return true;
        }
    }
    [HarmonyPatch(typeof(PlayerInventory))]
    public class PlayerInventoryPatch {
        [HarmonyPatch("RPC_ClearSlot")]
        [HarmonyPrefix]
        public static bool OnClearSlot(PlayerInventory __instance, int slotID) {
            if (!Plugin.IsHost) { return true; }
            FieldInfo field = ReflectionHelper.Field.GetField<PlayerInventory>("m_photonView");
            ACPlayer player = Plugin.GetFromInventoryView((PhotonView)field.GetValue(__instance));
            if (player.player == Player.localPlayer) {
                return true;
            }
            if (player == null) { Plugin.Log("PlayerFromInventoryView is null somehow"); }
            Item item = __instance.slots[slotID].ItemInSlot.item;
            if (item == null) {
                Trace.WriteLine("Player (" + player.GetName() + ") tried to clear an empty slot");
                return false;
            }
            player.cachedItems.AddFirst(item.id);
            return true;
        }
        [HarmonyPatch("RPC_AddToSlot")]
        [HarmonyPrefix]
        public static bool OnAddToSlot(PlayerInventory __instance, int slotID, byte itemID, byte[] data) {
            if (!Plugin.IsHost) { return true; }
            FieldInfo field = ReflectionHelper.Field.GetField<PlayerInventory>("m_photonView");
            ACPlayer player = Plugin.GetFromInventoryView((PhotonView)field.GetValue(__instance));
            if (player.player == Player.localPlayer) {
                return true;
            }
            foreach (Tuple<byte, byte[]> t in player.cachedPickup) {
                if (t.Item1 == itemID) {
                    player.cachedPickup.Remove(t);
                    return true;
                }
            }
            Trace.WriteLine("Player (" + player.GetName() + ") tried to add " + itemID + ", [" + string.Join(", ", data) + "]");
            return false;
        }
        [HarmonyPatch("RPC_SyncInventoryToOthers")]
        [HarmonyPrefix]
        public static bool Ignore(PlayerInventory __instance) {
            return !Plugin.IsHost;
        }
    }
    [HarmonyPatch(typeof(Player))]
    public class PlayerPatch {
        [HarmonyPatch("RPC_RequestCreatePickup")]
        [HarmonyPrefix]
        public static bool OnCreatePickup(Player __instance, byte itemID, byte[] dataBuffer, Vector3 pos, Quaternion rot) {
            return HandleCreatePickup(__instance, itemID);
        }
        [HarmonyPatch("RPC_RequestCreatePickupVel")]
        [HarmonyPrefix]
        public static bool OnCreatePickupVel(Player __instance, byte itemID, byte[] dataBuffer, Vector3 pos, Quaternion rot) {
            return HandleCreatePickup(__instance, itemID);
        }

        public static bool HandleCreatePickup(Player __instance, byte itemID) {
            if (!Plugin.IsHost) { return true;}
            if (__instance == Player.localPlayer) {
                return true;
            }
            ACPlayer player = Plugin.GetPlayer(__instance);
            PlayerInventory playerInventory = null; __instance.TryGetInventory(out playerInventory);
            if (playerInventory == null) {
                Plugin.Log("Player (" + player.GetName() + ") inventory is null somehow");
                return true;
            } else {
                if (player.cachedItems.Contains(itemID)) {
                    player.cachedItems.Remove(itemID);
                    return true;
                } else {
                    Trace.WriteLine("Player (" + player.GetName() + ") attempted to spawn item " + itemID);
                    return false;
                }
            }
        }
    }

    public class ACPlayer {
        public LinkedList<byte> cachedItems;
        public LinkedList<Tuple<byte, byte[]>> cachedPickup;
        public Player player = null;
        public int ActorNumber = 0;
        public ACPlayer(Player player) {
            this.player = player;
            ActorNumber = player.refs.view.Owner.ActorNumber;
            cachedItems = new LinkedList<byte>();
            cachedPickup = new LinkedList<Tuple<byte, byte[]>>();
        }
        public string GetName() {
            return player.refs.view.Owner.NickName;
        }
    }
}
