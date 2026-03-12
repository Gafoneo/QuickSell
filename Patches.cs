using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Unity.Netcode;
using UnityEngine;

namespace QuickSell;

public class HelperFuncs
{
    public static void OnLobbyEntrance()
    {
        QuickSell.Logger.LogDebug("Calling OnLobbyEntrance()");
        QuickSell.Logger.LogDebug("Creating allItems list");

        // Add all possible items to List<item name, prefab name, scan node name>
        QuickSell.allItems =
        [..
            Resources.FindObjectsOfTypeAll<Item>()
            .Where(i => i.spawnPrefab && "box" != i.itemName)
            .Select(i => (i.spawnPrefab.name, i.name, i.itemName, i.spawnPrefab.GetComponentInChildren<ScanNodeProperties>()?.headerText ?? ""))
        ];

        QuickSell.Logger.LogDebug($"allItems list created. Length: {QuickSell.allItems.Count}");
    }
    public static void TryClearDepositItemsDesk()
    {
        var desk = UnityEngine.Object.FindObjectOfType<DepositItemsDesk>();
        if (desk == null) return;

        desk.itemsOnCounter?.Clear();
    }

    public static void ListingAllScrap()
    {
        QuickSell.Logger.LogDebug("Listing all scrap on level load");
        Patches.scrapOnShip = UnityEngine.Object.FindObjectsOfType<GrabbableObject>().ToList() ?? [];
        QuickSell.Logger.LogDebug($"There is {Patches.scrapOnShip.Count} scrap on ship");
    }

    public static void AddPresent(GrabbableObject? component)
    {
        if (component == null)
        {
            QuickSell.Logger.LogDebug($"Component is null!!");
            return;
        }
        Patches.scrapOnShip.Add(component);
        QuickSell.Logger.LogDebug($"Added {component.name} worth {component.scrapValue}");
    }
}

public class Patches
{
    public static int valueOnDesk;
    public static List<GrabbableObject> scrapOnShip = [];

    [HarmonyPatch(typeof(RoundManager), nameof(RoundManager.GenerateNewLevelClientRpc))]
    public class ScrapListingClient
    {
        [HarmonyPrefix]
        static void ListingAllScrapClient()
        {
            HelperFuncs.ListingAllScrap();
        }
    }

    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.StartGame))]
    public class ScrapListingServer
    {
        [HarmonyPrefix]
        static void ListingAllScrapServer()
        {
            HelperFuncs.ListingAllScrap();
        }
    }


    [HarmonyPatch]
    class ListingGiftsClient
    {
        static MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("GiftBoxItem+<waitForGiftPresentToSpawnOnClient>d__19");
            return AccessTools.Method(type, "MoveNext");
        }

        [HarmonyFinalizer]
        static Exception Finalizer(Exception __exception, object __instance)
        {
            try
            {
                var stateType = __instance.GetType();
                var netObjectField = stateType.GetField("<netObject>5__2", BindingFlags.NonPublic | BindingFlags.Instance);
                var netObject = netObjectField?.GetValue(__instance) as NetworkObject;
                var component = netObject?.GetComponent<GrabbableObject>();

                HelperFuncs.AddPresent(component);
            }
            catch (Exception e)
            {
                QuickSell.Logger.LogError("Finalizer helper failed: " + e);
            }

            return null!; // suppress the original exception
        }
    }

    /*
    [HarmonyPatch]
    public class ListingGiftsClient
    {
        static MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("GiftBoxItem+<waitForGiftPresentToSpawnOnClient>d__19");
            return AccessTools.Method(type, "MoveNext");
        }
        
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var instructionList = new List<CodeInstruction>(instructions);

            var targetField = AccessTools.Field(
                typeof(GrabbableObject),
                nameof(GrabbableObject.reachedFloorTarget)
            );

            bool inserted = false;
            for (int i = 0; i < instructionList.Count; i++)
            {
                yield return instructionList[i];

                if (!inserted && instructionList[i].opcode == OpCodes.Stloc_2)
                {
                    yield return new CodeInstruction(OpCodes.Ldloc_2);
                    yield return new CodeInstruction(
                        OpCodes.Call,
                        AccessTools.Method(typeof(HelperFuncs), nameof(HelperFuncs.AddPresent))
                    );

                    inserted = true;
                }
            }
        }
    }
    */

    [HarmonyPatch(typeof(GiftBoxItem), nameof(GiftBoxItem.OpenGiftBoxServerRpc))]
    public class ListingGiftsServer
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = AccessTools.Method(typeof(GiftBoxItem), "OpenGiftBoxClientRpc");

            foreach (var instruction in instructions)
            {
                if (instruction.Calls(target))
                {
                    yield return new CodeInstruction(OpCodes.Ldloc_S, (byte)4);
                    yield return new CodeInstruction(
                        OpCodes.Call,
                        AccessTools.Method(typeof(HelperFuncs), nameof(HelperFuncs.AddPresent))
                    );
                }

                yield return instruction;
            }
        }
    }

    [HarmonyPatch(typeof(GameNetworkManager))]
    public class LobbyPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch("StartHost")]
        static void OnLobbyCreated()
        {
            HelperFuncs.OnLobbyEntrance();
            HelperFuncs.TryClearDepositItemsDesk();
            valueOnDesk = 0;
        }

        [HarmonyPostfix]
        [HarmonyPatch("StartClient")]
        static void OnLobbyJoined()
        {
            HelperFuncs.OnLobbyEntrance();
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

    /*
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
                    QuickSell.Logger.LogWarning($"ItemSaveData length mismatch: {itemSaveData.Length} < {expected}. Truncating.");
                }
            }
            catch (Exception e)
            {
                QuickSell.Logger.LogError($"[Patch] Error checking itemSaveData length: {e}");
            }
        }
    }
    */

    // [HarmonyPatch(typeof(MenuManager), "Start")]
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

    // [HarmonyPatch(typeof(MenuManager), "Start")]
    public static class Changelog
    {
        static void Postfix(MenuManager __instance)
        {
            QuickSell.Logger.LogDebug("Trying to create notification");
            __instance.DisplayMenuNotification("Some placeholder info", "[ OK ]");
            var canvas = UnityEngine.Object
                .FindObjectsOfType<Canvas>(true)
                .FirstOrDefault(c => c.name.Contains("Canvas"));

            if (canvas == null)
            {
                QuickSell.Logger.LogDebug("Canvas is null, shutting down");
                return;
            }

            RectTransform[] transforms = canvas.GetComponentsInChildren<RectTransform>();

            RectTransform? panel = transforms.Where(i => i.name == "Panel" && i.transform.parent?.name == "MenuNotification").FirstOrDefault();
            RectTransform? image = transforms.Where(i => i.name == "Image" && i.transform.parent?.name == "Panel").FirstOrDefault();


            if (panel == null)
            {
                QuickSell.Logger.LogDebug("Panel is null");
                return;
            }

            panel.sizeDelta = new Vector2(
                panel.sizeDelta.x * 2f,
                panel.sizeDelta.y * 2.5f
            );

            image.sizeDelta = new Vector2(
                image.sizeDelta.x * 2f,
                image.sizeDelta.y * 2.5f
            );

            QuickSell.Logger.LogDebug("Panel resized successfully");
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
