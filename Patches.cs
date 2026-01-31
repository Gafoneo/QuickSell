using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using System.Reflection;
using System.Reflection.Emit;

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

    [HarmonyPatch(typeof(MenuManager), "Start")]
    public static class NoWrongVersionPopup
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = new List<CodeInstruction>(instructions);

            var displayNotif = typeof(MenuManager).GetMethod(
                "DisplayMenuNotification",
                BindingFlags.Instance | BindingFlags.Public
            );

            int callIndex = -1;
            for (int i = 0; i < code.Count; i++)
            {
                if (!(code[i].operand is string str && str.Contains("Some of your save files may not be compatible with version"))) continue;

                for (int j = i; j < code.Count; j++)
                {
                    if (code[j].Calls(displayNotif))
                    {
                        callIndex = j;
                        break;
                    }
                }
                break;
            }

            if (callIndex == -1) return code;

            for (int i = callIndex; i >= 0; i--)
            {
                if (code[i].operand is string str && str.Contains("Some of your save files may not be compatible with version"))
                {
                    code[i].opcode = OpCodes.Nop;
                    if (i >= 1 && code[i - 1].opcode == OpCodes.Ldarg_0) code[i - 1].opcode = OpCodes.Nop;
                    break;
                }
                code[i].opcode = OpCodes.Nop;
            }

            return code;
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
