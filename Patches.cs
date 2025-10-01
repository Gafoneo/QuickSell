using HarmonyLib;
using Unity.Netcode;

namespace QuickSell;

public class Patches
{
    public static int valueOnDesk;

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
}
