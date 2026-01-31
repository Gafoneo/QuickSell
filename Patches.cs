using HarmonyLib;
using System;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace QuickSell;

public class HelperFuncs
{
    public static void TryClearDepositItemsDesk()
    {
        var desk = UnityEngine.Object.FindObjectOfType<DepositItemsDesk>();
        if (desk == null) return;

        desk.itemsOnCounter?.Clear();
    }
}

public class Patches
{
    public static int valueOnDesk;

    [HarmonyPatch(typeof(GameNetworkManager))]
    public class LobbyPatches
    {
        [HarmonyPatch("StartHost")]
        [HarmonyPostfix]
        static void OnLobbyCreated()
        {
            QuickSell.OnLobbyEntrance();
            HelperFuncs.TryClearDepositItemsDesk();
            valueOnDesk = 0;
        }

        [HarmonyPatch("StartClient")]
        [HarmonyPostfix]
        static void OnLobbyJoined()
        {
            QuickSell.OnLobbyEntrance();
            HelperFuncs.TryClearDepositItemsDesk();
            valueOnDesk = 0;
        }
    }

    // Thanks to NutNutty for these patches:
    [HarmonyPatch(typeof(DepositItemsDesk))]
    public class DepositItemsDeskPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch("AddObjectToDeskClientRpc")]
        public static void FetchValue(ref DepositItemsDesk __instance)
        {
            if (!NetworkManager.Singleton.IsServer && NetworkManager.Singleton.IsClient)
            {
                NetworkObject networkObject = __instance.lastObjectAddedToDesk;
                __instance.itemsOnCounter.Add(networkObject.GetComponentInChildren<GrabbableObject>());
            }
            int num = 0;
            for (int i = 0; i < __instance.itemsOnCounter.Count; i++)
            {
                if (__instance.itemsOnCounter[i].itemProperties.isScrap)
                {
                    num += __instance.itemsOnCounter[i].scrapValue;
                }
            }

            valueOnDesk = (int)((float)num * StartOfRound.Instance.companyBuyingRate);
            QuickSell.Logger.LogDebug($"Value on desk: {valueOnDesk}");
        }

        [HarmonyPostfix]
        [HarmonyPatch("SellItemsClientRpc")]
        public static void ClearValue(ref DepositItemsDesk __instance)
        {
            if (!NetworkManager.Singleton.IsServer && NetworkManager.Singleton.IsClient)
            {
                __instance.itemsOnCounter.Clear();
            }
            valueOnDesk = 0;
            QuickSell.Logger.LogDebug($"Value on desk: {valueOnDesk}");
        }


    }
    
    // I don't remember what it's really for but if I wrote it, maybe it's needed for something; maybe I'll delete it in the future
    [HarmonyPatch(typeof(StartOfRound), "SyncShipUnlockablesClientRpc")]
    public static class FixItemSaveDataMismatch
    {
        [HarmonyPrefix]
        public static void SafeLoadData(int[] itemSaveData)
        {
            try
            {
                var objs = UnityEngine.Object.FindObjectsByType<GrabbableObject>(
                    FindObjectsInactive.Exclude, FindObjectsSortMode.None);

                int expected = objs.Count(o => o.itemProperties.saveItemVariable);
                if (itemSaveData.Length < expected)
                {
                    Debug.LogWarning($"ItemSaveData length mismatch: {itemSaveData.Length} < {expected}. Truncating.");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[Patch] Error checking itemSaveData length: {e}");
            }
        }
    }

    // WIP for opening gifts
    // [HarmonyPatch(typeof(GameNetcodeStuff.PlayerControllerB))]
    // public static class StopClientRPCWhenNeeded
    // {
    //     [HarmonyPrefix]
    //     static bool GrabObjectClientRpc(bool grabValidated, NetworkObjectReference grabbedObject)
    //     {
    //         if (!QuickSell.Instance.openingGifts) return true;
    // 
    //         QuickSell.Instance.grabRPC1 = true;
    //         return false;
    //     }
    // 
    //     [HarmonyPrefix]
    //     static bool GrabServerRpc()
    //     {
    //         if (!QuickSell.Instance.openingGifts) return true;
    // 
    //         QuickSell.Instance.grabRPC2 = true;
    //         return false;
    //     }
    // }
}
