using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using ChatCommandAPI;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace QuickSell;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("baer1.ChatCommandAPI")]
public class QuickSell : BaseUnityPlugin
{
    public static QuickSell Instance { get; private set; } = null!;
    internal static new ManualLogSource Logger { get; private set; } = null!;

    private ConfigEntry<string> itemBlacklist = null!;
    private ConfigEntry<string> priorityItems = null!;
    internal string[] ItemBlacklist => CommaSplit(itemBlacklist.Value);
    internal string[] PriorityItems => CommaSplit(priorityItems.Value);

    private void Awake()
    {
        Logger = base.Logger;
        Instance = this;

        itemBlacklist = Config.Bind(
            "Items",
            "ItemBlacklist",
            CommaJoin(["ShotgunItem", "KnifeItem", "ZeddogPlushie", "GiftBox"]),
            "Items to never sell by internal name (comma-separated)"
        );
        priorityItems = Config.Bind(
            "Items",
            "PriorityItems",
            CommaJoin(["Clock", "EasterEgg", "SoccerBall", "WhoopieCushion"]),
            "Items which are prioritized when selling"
        );

        _ = new SellCommand();
        _ = new OvertimeCommand();
        var harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);

        harmony.PatchAll();

        Logger.LogInfo($"{MyPluginInfo.PLUGIN_GUID} v{MyPluginInfo.PLUGIN_VERSION} has loaded!");
    }

    private static string CommaJoin(string[] i) => i.Join(delimiter: ",");

    private static string[] CommaSplit(string i) => [.. i.Split(',').Select(i => i.Trim())];
}
 
public class SellCommand : Command
{
    public override string Name => "Sell";
    public override string Description => "Sells items, use /sell help to see available uses." +
        "If you find any bugs, inaccuracies or have any improvements in mind please open an issue on github (the link is on the QuickSell's modpage).";
    public override string[] Syntax => ["", "help [flags]", "item [item]", "{ quota | all } [-a]", "<value> [-o] [-t] [-a] [-n]"];

    protected struct SellData()
    {
        // Values
        public string error = "If you got this error something really strange happend. Please let me know about it";  // An error code for ChatCommandAPI
        public string[] args = [];  // All original arguments given with the command
        public string variation = "";  // The command variation
        public int value = 0;  // Sum of values of items that should be on the counter
        public string originalValue = "";  // The requested value by the user (after expression evaluation)
        public int quotaLeft = 0;  // Unfullfilled quota
        public int existingMoney = 0;  // The existing money that we need to account for (money in terminal or/and existing overtime)

        // Flags
        public bool o = false;  // Check for overtime
        public bool t = false;  // Account for existing money
        public bool a = false;  // Ignore blacklist
        public bool n = false;  // Force calculations to think that there was no restart before selling even if the client thinks otherwise
    }

    protected static SellData sellData;

    public override bool Invoke(string[] args, Dictionary<string, string> kwargs, out string err)
    {
        try
        {
            QuickSell.Logger.LogDebug("The sell command was initiated");

            sellData = new() { args = args };

            if (args.Length == 0) return SellNoArgs();

            // Parse the variation and the flags from the arguments
            ParseArguments();

            if (sellData.variation == "help") return SellHelp();
            if (sellData.variation == "item") return SellParticularItem();
            if (sellData.variation == "quota") return SellQuota();
            if (sellData.variation == "all") return SellAll();
            if (sellData.variation == "amount") return SellForRequestedAmount();

            sellData.error = "No variation was specified?..";
            return false;
        }
        finally
        {
            err = sellData.error;
        }
    }

    protected static bool SellNoArgs()
    {
        QuickSell.Logger.LogDebug("Got no arguments -> calling SellNoArgs");

        if (!OpenDoor(out int itemCount, out int totalValue)) return false;

        ChatCommandAPI.ChatCommandAPI.Print($"Selling {NumberOfItems(itemCount)} with a total value of {ValueOfItems(totalValue)}");
        return true;
    }

    protected static bool SellHelp()
    {
        QuickSell.Logger.LogDebug("variation == \"help\" -> calling SellHelp()");
        if (sellData.args.Length <= 1)
        {
            QuickSell.Logger.LogDebug("No more arguments -> printing default help page");
            ChatCommandAPI.ChatCommandAPI.Print(
                "========== HELP PAGE ==========\n" +
                "Usage: /sell <variation> [flags]\n\n" +

                "Command variations:\n" +
                "\"help\" to open this page or a specific help page \n" +
                "\"item\" to sell all items like the one you are holding or the one you specified\n" +
                "\"quota\" to sell exactly for quota\n" +
                "\"all\" to sell all unfiltered scrap available\n" +
                "<amount> (input a number instead of \"amount\") to sell exactly how much you need\n\n" +

                "Use \"/sell help pages\" to see info on all pages\n" +
                "Use \"/sell help flags\" to see info on important flags\n" +
                "Use \"/sell help <variation>\" to see info on specific command\n" +
                "==============================="
            );
            return true;
        }

        string page = sellData.args[1].ToLower();

        if (page == "pages" || page == "help")
        {
            ChatCommandAPI.ChatCommandAPI.Print(
                "======= PAGES HELP PAGE =======\n" +
                "Usage: /sell help [page]\n\n" +

                "Pages:\n" +
                "pages, flags, item, quota, all, amount, -o, -t, -a, -n\n" +
                "==============================="
            );
            return true;
        }
        if (page == "flags")
        {
            ChatCommandAPI.ChatCommandAPI.Print(
                "======= FLAG HELP PAGE ========\n" +
                "Usage: /sell <variation> [flags]\n\n" +

                "Combining flags:\n" +
                "Split: /sell <variation> -t -o -a\n" +
                "Together: /sell <variation> -toa\n\n" +

                "\"-o\" to sell accounting for overtime (used with <amount>)\n" +
                "\"-t\" for accounting for existing money in terminal and overtime (used with <amount>)\n" +
                "\"-a\" to ignore blacklist (used with quota, all, <amount>)\n" +
                "\"-n\" to force non-restart overtime calculations (needed in rare edge cases)\n\n" +

                "Use \"/sell help <flag>\" to see info on specific flag\n" +
                "=============================="
            );
            return true;
        }
        if (page == "item" || page == "items")
        {
            ChatCommandAPI.ChatCommandAPI.Print(
                "======= ITEM HELP PAGE ========\n" +
                "Usage: /sell item [item]\n\n" +

                "Sells all items with the specified name. If no name was specified then checks what item you are holding and gets it's name instead (and sells this held item too)\n" +
                "==============================="
            );
            return true;
        }
        if (page == "quota")
        {
            ChatCommandAPI.ChatCommandAPI.Print(
                "======= QUOTA HELP PAGE =======\n" +
                "Usage: /sell quota [-a]\n\n" +

                "Checks how much quota is left and tries to sell exactly that (if it's not enough, nothing will be sold and if exact value isn't achievable sells the smallest value after that)\n" +
                "==============================="
            );
            return true;
        }
        if (page == "all")
        {
            ChatCommandAPI.ChatCommandAPI.Print(
                "======== ALL HELP PAGE ========\n" +
                "Usage: /sell all [-a]\n\n" +

                "Sells all (non-blacklisted, use -a to ignore blacklist) items\n" +
                "==============================="
            );
            return true;
        }
        if (page == "amount" || page == "<amount>")
        {
            ChatCommandAPI.ChatCommandAPI.Print(
                "===== AMOUNT HELP PAGE =====\n" +
                "Usage: /sell <amount> [-o] [-t] [-a] [-n]\n\n" +

                "Tries to sell exactly how much you specified. If there is not enough scrap, sells nothing. If an exact value isn't achievable sells the smallest value after that\n" +
                "==============================="
            );
            return true;
        }
        if (page == "-o")
        {
            ChatCommandAPI.ChatCommandAPI.Print(
                "======== -o HELP PAGE =========\n" +
                "Usage: /sell <amount> -o\n\n" +

                "Respects the fact that your sold items can cause overtime and includes it in the calculations " +
                "(note that the overtime caused by already sold items isn't included, you need -t flag for that) so that:\n" +
                "requested value = final value in terminal (after leaving the planet) - existing money (look into -t help page for that)\n" +
                "==============================="
            );
            return true;
        }
        if (page == "-t")
        {
            ChatCommandAPI.ChatCommandAPI.Print(
                "======== -t HELP PAGE =========\n" +
                "Usage: /sell <amount> -t\n\n" +

                "Removes existing money (already existing credits in terminal, items on desk and, if -o flag is present, future overtime based on these two) from your requsted value so that:\n" +
                "requested value = final value in terminal (after leaving the planet) = existing money + sold items (+ overtime caused by sold items if -o flag is present)\n" +
                "==============================="
            );
            return true;
        }
        if (page == "-a")
        {
            ChatCommandAPI.ChatCommandAPI.Print(
                "======== -a HELP PAGE =========\n" +
                "Usage: /sell <quota | all | amount> -a\n\n" +

                "When trying to find right items to sell, ignores blacklist so that all items can be sold\n" +
                "==============================="
            );
            return true;
        }
        if (page == "-n")
        {
            ChatCommandAPI.ChatCommandAPI.Print(
                "======== -n HELP PAGE =========\n" +
                "Usage: /sell <amount> -n\n\n" +

                "Forces EVERY overtime calculation that occures during the execution of THIS command to think that there was no rehost after the final day of this quota, even if there was one). " +
                "It is only needed if a host has a mod for late joining (aka LateCompany) and you joined after the final day of this quota (your client will think that there was a rehost then). " +
                "There is no way (that I know of, at least, if you know one please tell me) to check if there was or wasn't a real rehost in this case, and if there wasn't, then all overtime " +
                "calculations will be 15 smaller. This flag accounts for that, but note that if the rehost has actually occured and you used this flag then all overtime calculation will be " +
                "15 bigger so you should ask your host if they have done a rehost or not to get it right\n" +
                "==============================="
            );
            return true;
        }

        sellData.error = "No page with this name exists";
        return false;
    }

    protected static bool SellParticularItem()
    {
        QuickSell.Logger.LogDebug("variation == \"item\" -> calling SellParticularItem()");

        string itemName;
        if (sellData.args.Length == 1)
        {
            var player = StartOfRound.Instance.localPlayerController;

            if (player == null)
            {
                QuickSell.Logger.LogDebug("localPlayerController == null -> returning false");
                sellData.error = "localPlayerController == null";
                return false;
            }

            var heldItem = player.ItemSlots[player.currentItemSlot];

            if (heldItem == null || heldItem.name == "")
            {
                QuickSell.Logger.LogDebug("No item is held and no item was specified");
                sellData.error = "No item is held and no item was specified";
                return false;
            }

            itemName = RemoveClone(heldItem.name);
            QuickSell.Logger.LogDebug($"Item to sell: {itemName}");

            QuickSell.Logger.LogDebug("Dropping held item");
            player.DiscardHeldObject();
        }
        else
        {
            itemName = sellData.args[1];
            QuickSell.Logger.LogDebug($"Item to sell: {itemName}");
        }

        if (!FindDesk(out var desk)) return false;

        var items = Object.FindObjectsOfType<GrabbableObject>();
        if (items == null || items.Length == 0)
        {
            QuickSell.Logger.LogDebug("No items were found");
            sellData.error = "No items were found";
            return false;
        }

        items = FindItems(items, itemName);
        if (items == null || items.Length == 0)
        {
            QuickSell.Logger.LogDebug($"No items called \"{itemName}\" were detected");
            sellData.error = $"No items called \"{itemName}\" were detected";
            return false;
        }

        if (!SellItems([.. items], out int itemCount)) return false;

        if (!desk.doorOpen)
        {
            QuickSell.Logger.LogDebug("The door is not open -> opening it");
            desk.SetTimesHeardNoiseServerRpc(5f);
        }

        // The printout of the selling results
        ChatCommandAPI.ChatCommandAPI.Print(
            $"==============================\n" +
            $"Selling {NumberOfItems(itemCount)} named \"{itemName}\" with a total value of {ValueOfItems([.. items])}" +
            $"\n=============================="
        );

        QuickSell.Logger.LogDebug("The sell command completed it's job, terminating");
        return true;
    }

    protected static bool SellQuota()
    {
        QuickSell.Logger.LogDebug("variation == \"quota\" -> calling SellQuota()");

        sellData.value = TimeOfDay.Instance.profitQuota - (TimeOfDay.Instance.quotaFulfilled + Patches.valueOnDesk);
        sellData.originalValue = sellData.value.ToString();
        QuickSell.Logger.LogDebug($"The requested value (profitQuota - (quotaFulfilled + value on desk)): {sellData.value}");

        if (sellData.value < 1)
        {
            ChatCommandAPI.ChatCommandAPI.Print(
                $"===============================\n" +
                $"Quota is already fulfilled\n" +
                $"==============================="
            );
            QuickSell.Logger.LogDebug("Quota is already fulfilled -> nothing left to do");
            return true;
        }

        return SellForValue();
    }

    protected static bool SellAll()
    {
        QuickSell.Logger.LogDebug("variation == \"all\" -> calling SellAll()");
        sellData.value = -1;

        return SellForValue();
    }

    protected static bool SellForRequestedAmount()
    {
        QuickSell.Logger.LogDebug($"variation == \"<amount>\" -> calling SellForRequestedAmount()");

        // Add every argument except the flags to the expression
        string expression = string.Join(' ', sellData.args.Where(i => i.First() != '-' || i.Length <= 1)).Trim();
        QuickSell.Logger.LogDebug($"Expression: \"{expression}\" ");

        // Attempts to compute an expression
        QuickSell.Logger.LogDebug($"Evaluating expression");
        string evaluatedExpression = "";
        try
        {
            evaluatedExpression = new DataTable().Compute(expression, "").ToString();
        }
        catch
        {
            QuickSell.Logger.LogDebug("Failed to evalute expression");
            sellData.error = "Failed to evalute expression";
            return false;
        }

        // Two checks for value being right
        if (!int.TryParse(evaluatedExpression, out sellData.value) || sellData.value < 0)
        {
            QuickSell.Logger.LogDebug("The value is not convertable into integer");
            sellData.error = "The value is not convertable into integer";
            return false;
        }
        if (sellData.value < 0)
        {
            QuickSell.Logger.LogDebug("The value must be positive");
            sellData.error = "The value must be positive";
            return false;
        }
        QuickSell.Logger.LogDebug($"Expression evaluated: {expression} => {evaluatedExpression}");

        // Assigning the result of the expression as the requested value
        sellData.originalValue = sellData.value.ToString();

        // Logic for accounting for money in terminal and already existing overtime
        if (sellData.t)
        {
            QuickSell.Logger.LogDebug($"Entering logic for accounting for existing money");

            var terminal = UnityEngine.Object.FindObjectOfType<Terminal>();
            if (terminal == null)
            {
                sellData.error = "Cannot find terminal!";
                QuickSell.Logger.LogDebug($"Cannot find terminal!");
                return false;
            }

            int credits = Traverse.Create(terminal).Field("groupCredits").GetValue<int>();
            QuickSell.Logger.LogDebug($"Credits in terminal: {credits}");
            if (sellData.o)
            {
                QuickSell.Logger.LogDebug($"Accounting for both terminal money and existing overtime");
                sellData.existingMoney = credits + Patches.valueOnDesk + FindOvertime(TimeOfDay.Instance.quotaFulfilled + Patches.valueOnDesk, TimeOfDay.Instance.profitQuota, sellData.n);
                sellData.value -= sellData.existingMoney;
                QuickSell.Logger.LogDebug($"Overall existing money: {sellData.existingMoney}");
                QuickSell.Logger.LogDebug($"Adjusted value: {sellData.value}");
            }
            else
            {
                QuickSell.Logger.LogDebug($"Accounting just for terminal money");
                sellData.existingMoney = credits + Patches.valueOnDesk;
                sellData.value -= sellData.existingMoney;
                QuickSell.Logger.LogDebug($"Money in terminal: {sellData.existingMoney}");
                QuickSell.Logger.LogDebug($"Adjusted value: {sellData.value}");
            }

            // Check if after accounting for existing money there is no need to sell anything
            if (sellData.value <= 0)
            {
                QuickSell.Logger.LogDebug($"{sellData.value} <= 0 -> There is no need to sell anything, terminating");
                ChatCommandAPI.ChatCommandAPI.Print($"You already have {sellData.existingMoney} existing money out of desired {sellData.originalValue}");
                return true;
            }

        }

        // Overtime calculations
        if (sellData.o)
        {
            QuickSell.Logger.LogDebug($"Entering logic for overtime calculations");

            sellData.quotaLeft = TimeOfDay.Instance.profitQuota - (TimeOfDay.Instance.quotaFulfilled + Patches.valueOnDesk);
            QuickSell.Logger.LogDebug($"Quota left: {sellData.quotaLeft}");

            sellData.value = FindValueWithOvertime(sellData.value, sellData.quotaLeft, sellData.n);

            QuickSell.Logger.LogDebug($"New value before overtime: {sellData.value}");
        }

        return SellForValue();
    }

    // Unites the whole (before, while and after) sell process (if there is a resulting value which we need to get) itself after the needed value has been found
    protected static bool SellForValue()  // Change calculated overtime so it uses actual sold value instead of requested
    {
        QuickSell.Logger.LogDebug($"Calling SellForValue({sellData.value})");

        if (!FindDesk(out var desk)) return false;

        var items = ItemsForValue(sellData.value, sellData.a);
        if (items == null)
        {
            QuickSell.Logger.LogDebug("Got null from ItemsForValue() -> not selling anything");
            sellData.error = "No items were found";
            return false;
        }

        if (items.Count == 0)
        {
            QuickSell.Logger.LogDebug("The list of items you need to sell is empty so you probably can't afford the amount you requested. If not, please report this.");
            sellData.error = "You can't afford to sell that amount";
            return false;
        }

        if (!SellItems(items, out int itemCount))
        {
            QuickSell.Logger.LogDebug("Error selling items");
            sellData.error = "Error selling items";
            return false;
        }

        if (!desk.doorOpen)
        {
            QuickSell.Logger.LogDebug("The door is not open -> opening it");
            desk.SetTimesHeardNoiseServerRpc(5f);
        }
        else
        {
            QuickSell.Logger.LogDebug("The door is open -> starting coroutine to check later if the items are still on desk");
            StartOfRound.Instance.StartCoroutine(DelayedDeskCheck());
        }

        // Calculates overtime addition caused by this sell command
        int calculatedOvertime = sellData.o || sellData.variation == "all" ? FindOvertime(items.Sum(obj => obj.scrapValue), sellData.quotaLeft, sellData.n) : 0;

        // The printout of the selling results
        ChatCommandAPI.ChatCommandAPI.Print(
            $"==============================\n" +

            $"Selling {NumberOfItems(itemCount)} with a total value of {ValueOfItems(items)}" +

            $"{(
                calculatedOvertime != 0
                ? $" + {calculatedOvertime} overtime"
                : ""
            )}" +

            $"{(
                sellData.t
                ? $" + {sellData.existingMoney} existing money"
                : ""
            )}" +

            $"{(
                sellData.originalValue != ""
                ? $":\n{items.Sum(obj => obj.scrapValue) + calculatedOvertime + sellData.existingMoney} sold / {sellData.originalValue} requested"
                : ", sold every unfiltered item"
            )}" +

            $"\n=============================="
        );

        QuickSell.Logger.LogDebug("The sell command completed it's job, terminating");
        return true;
    }

    protected static List<GrabbableObject>? ItemsForValue(int value, bool ignoreBlacklist)
    {
        QuickSell.Logger.LogDebug($"Calling ItemsForValue({value})");

        if (!FindDesk(out var desk)) QuickSell.Logger.LogDebug("A desk was not found, not a critical error, continuing");

        if (value is 0)
        {
            QuickSell.Logger.LogDebug("value == 0 -> returning null");
            return null;
        }

        var items = Object.FindObjectsOfType<GrabbableObject>();
        if (items == null || items.Length == 0)
        {
            QuickSell.Logger.LogDebug("No items were found -> returning null");
            return null;
        }
        QuickSell.Logger.LogDebug($"Cost of all scrap: {items.Sum(i => i.scrapValue)}, {NumberOfItems(items.Count())}");

        items = FilterItems(items, ignoreBlacklist);
        if (items.Length == 0)
        {
            QuickSell.Logger.LogDebug("No items were left after filtering -> returning null");
            return null;
        }
        QuickSell.Logger.LogDebug($"Cost of all unfiltered scrap (max possible value): {items.Sum(i => i.scrapValue)}, {NumberOfItems(items.Count())}");

        if (value == -1)
        {
            QuickSell.Logger.LogDebug("value == -1 -> need to sell everything -> returning [.. items]");
            return items.Length != 0 ? [.. items] : null;
        }

        value = (int)Math.Ceiling((double)(value / StartOfRound.Instance.companyBuyingRate));
        QuickSell.Logger.LogDebug($"Value after accounting for buying rate: {value}");

        var minScrapItem = items.OrderBy(i => i.scrapValue).First();
        QuickSell.Logger.LogDebug($"The cheapest item is {minScrapItem.scrapValue}");
        if (value <= minScrapItem.scrapValue)
        {
            QuickSell.Logger.LogDebug($"value <= {minScrapItem.scrapValue} -> returning the cheapest item");
            return [minScrapItem];
        }


        if (items.Sum(i => i.scrapValue) < value)
        {
            QuickSell.Logger.LogDebug("Max possible value is not enough to get to the desired value -> returning empty list");
            return [];
        }

        var bestSubset = CombinationFinder(items, value);

        QuickSell.Logger.LogDebug($"ItemsForValue() returns a list of {NumberOfItems(bestSubset.Count())} worth {ValueOfItems(bestSubset)}");
        return bestSubset;
    }

    protected static List<GrabbableObject> CombinationFinder(GrabbableObject[] items, int value)
    {
        QuickSell.Logger.LogDebug($"Calling CombinationFinder({NumberOfItems(items.Count())}, {value})");

        List<GrabbableObject> bestSubset = [];
        int maxPossibleValue = items.Sum(item => item.scrapValue);
        bool[] reachable = new bool[maxPossibleValue + 1];
        List<GrabbableObject>[] dpItems = new List<GrabbableObject>[maxPossibleValue + 1];
        int[] dpPriorityCount = new int[maxPossibleValue + 1];

        for (int i = 0; i <= maxPossibleValue; i++)
        {
            reachable[i] = false;
            dpItems[i] = [];
            dpPriorityCount[i] = 0;
        }

        reachable[0] = true; // Even with no items on ship 0 value would be reachable

        QuickSell.Logger.LogDebug($"Items with priority: {QuickSell.Instance.PriorityItems.Join(delimiter: ", ")}");
        QuickSell.Logger.LogDebug("Starting looping through every item");
        foreach (var item in items)
        {
            int itemPriorityValue = IsPriority(item) ? 1 : 0;
            QuickSell.Logger.LogDebug($"Item: {RemoveClone(item.name)}, price: {item.scrapValue}, priority: {itemPriorityValue}");

            for (int j = maxPossibleValue; j >= item.scrapValue; j--)
            {
                int remainingValue = j - item.scrapValue;
                if (reachable[remainingValue])
                {
                    int newPriorityCount = dpPriorityCount[remainingValue] + itemPriorityValue;

                    if (!reachable[j] || newPriorityCount > dpPriorityCount[j])
                    {
                        reachable[j] = true;
                        dpItems[j] = [.. dpItems[remainingValue], item];
                        dpPriorityCount[j] = newPriorityCount;
                    }
                }
            }
        }

        if (reachable[value])
        {
            QuickSell.Logger.LogDebug($"CombinationFinder() has found a perfect combination for {value} (there still can be off-by-one errors because of how overtime works)");
            bestSubset = dpItems[value];
            return bestSubset;
        }

        for (int i = value + 1; i <= maxPossibleValue; i++)
        {
            if (reachable[i])
            {
                QuickSell.Logger.LogDebug($"CombinationFinder() has found the best imperfect combination: {i}/{value}");
                bestSubset = dpItems[i];
                return bestSubset;
            }
        }

        QuickSell.Logger.LogDebug($"CombinationFinder() failed to execute for unknown reason. Please report this to the mod author.");
        return [];
    }

    protected static bool SellItems(List<GrabbableObject> items, out int itemCount)
    {
        QuickSell.Logger.LogDebug($"Calling SellItems({NumberOfItems(items.Count())})");
        itemCount = 0;
        if (!FindDesk(out var desk)) return false;

        QuickSell.Logger.LogDebug($"Looping through every item");
        foreach (var i in items)
        {
            if (i == null)
                continue;
            QuickSell.Logger.LogDebug($"Item: {i.name}, price: {i.scrapValue}");
            itemCount++;

            var vector = RoundManager.RandomPointInBounds(desk.triggerCollider.bounds);
            vector.y = desk.triggerCollider.bounds.min.y;
            if (
                Physics.Raycast(
                    new Ray(vector + Vector3.up * 3f, Vector3.down),
                    out var hitInfo,
                    8f,
                    1048640,
                    QueryTriggerInteraction.Collide
                )
            )
            {
                vector = hitInfo.point;
            }

            vector.y += i.itemProperties.verticalOffset;
            vector = desk.deskObjectsContainer.transform.InverseTransformPoint(vector);

            desk.AddObjectToDeskServerRpc(i.NetworkObject);
            GameNetworkManager.Instance.localPlayerController.PlaceGrabbableObject(
                desk.deskObjectsContainer.transform,
                vector,
                false,
                i
            );
            GameNetworkManager.Instance.localPlayerController.PlaceObjectServerRpc(
                i.NetworkObject,
                desk.deskObjectsContainer,
                vector,
                false
            );
        }


        QuickSell.Logger.LogDebug($"Selling completed");
        return true;
    }

    protected static bool ParseArguments()
    {
        QuickSell.Logger.LogDebug("Calling ParseArguments()");

        // Parsing variation
        string probableVariation = sellData.args[0].ToLower();

        if (new string[] { "help" }.Contains(probableVariation)) sellData.variation = "help";
        else if (new string[] { "item", "items", "scrap" }.Contains(probableVariation)) sellData.variation = "item";
        else if (new string[] { "quota" }.Contains(probableVariation)) sellData.variation = "quota";
        else if (new string[] { "all" }.Contains(probableVariation)) sellData.variation = "all";
        else sellData.variation = "amount";

        QuickSell.Logger.LogDebug($"variation: {(sellData.variation == "amount" ? "no variation keyword was used so assuming <amount>" : sellData.variation)}");

        // Parsing flags
        string flags = string.Join("", sellData.args.Where(i => i.First() == '-' && i.Length > 1).Select(i => i[1..]));

        // Turn all the flags into variables for readability
        sellData.t = flags.Contains("t");
        sellData.o = flags.Contains("o");
        sellData.a = flags.Contains("a");
        sellData.n = flags.Contains("n");
        QuickSell.Logger.LogDebug($"Flags: -t == {sellData.t}; -o == {sellData.o}; -a == {sellData.a}; -n == {sellData.n}.");

        return true;
    }

    protected static bool OpenDoor(out int itemCount, out int totalValue)
    {
        QuickSell.Logger.LogDebug("Calling OpenDoor()");

        itemCount = 0;
        totalValue = 0;

        if (!FindDesk(out var desk)) return false;

        itemCount = desk.itemsOnCounter.Count;
        totalValue = desk.itemsOnCounter.Sum(i => i.scrapValue);
        QuickSell.Logger.LogDebug($"There are {NumberOfItems(itemCount)} on the desk worth {ValueOfItems(totalValue)}");
        if (itemCount == 0)
        {
            QuickSell.Logger.LogDebug("No items on the desk");
            sellData.error = "No items on the desk";
            return false;
        }
        
        if (desk.doorOpen)
        {
            QuickSell.Logger.LogDebug("Door was already open -> nothing left to do");
            sellData.error = "Door already open";
            return false;
        }

        QuickSell.Logger.LogDebug("Opening a door");
        desk.SetTimesHeardNoiseServerRpc(5f);
        return true;
    }

    public static IEnumerator DelayedDeskCheck()
    {
        QuickSell.Logger.LogDebug("Calling DelayedDeskCheck() (DDS)");

        if (!FindDesk(out var desk))
        {
            QuickSell.Logger.LogDebug("DDS: No desk was found, terminating");
            yield break;
        }

        yield return new WaitForSeconds(10f);
        QuickSell.Logger.LogDebug("DDS: 10 seconds passed");

        if (desk.itemsOnCounter.Count <= 0)
        {
            QuickSell.Logger.LogDebug("DDS: Desk still has no items, terminating");
            yield break;
        }
        QuickSell.Logger.LogDebug("DDS: Desk still has items");

        if (desk.doorOpen)
        {
            QuickSell.Logger.LogDebug("Door was already open, terminating");
            yield break;
        }

        QuickSell.Logger.LogDebug("Opening a door");
        desk.SetTimesHeardNoiseServerRpc(5f);
    }

    protected internal static int GetDeadline(bool forceNonRestart = false)
    {
        QuickSell.Logger.LogDebug($"Calling GetDeadline({forceNonRestart})");

        int realDeadline;
        realDeadline = TimeOfDay.Instance.globalTimeAtEndOfDay == 0.0 && !forceNonRestart ? TimeOfDay.Instance.daysUntilDeadline : TimeOfDay.Instance.daysUntilDeadline - 1;
        QuickSell.Logger.LogDebug($"Real days until deadline: {realDeadline}");
        return realDeadline;
    }

    // Finds a value it needs to sell so it will get you the desired value with overtime
    protected static int FindValueWithOvertime(int a, int q, bool forceNonRestart = false)
    {
        QuickSell.Logger.LogDebug($"Calling FindValueWithOvertime({a}, {q}, {forceNonRestart})");

        int realDeadline = GetDeadline(forceNonRestart); ;

        int value;
        if (a < q) value = a;
        else if (q >= 0) value = (int)Math.Ceiling((double)(5 * a + Math.Max(q - 75 * realDeadline, 0)) / 6);
        else value = (int)Math.Ceiling((double)(5 * a + Math.Max(q - 75 * realDeadline, q % 5)) / 6);

        QuickSell.Logger.LogDebug($"FindValueWithOvertime() returns value: {value}");
        return value;
    }

    protected static int FindOvertime(int x, int q, bool forceNonRestart = false)
    {
        QuickSell.Logger.LogDebug($"Calling FindOvertime({x}, {q}, {forceNonRestart})");

        int realDeadline = GetDeadline(forceNonRestart);
        int overtime = (q >= 0) ? Math.Max((x + Math.Min(75 * realDeadline - q, 0)) / 5, 0) : Math.Max((x + Math.Min(75 * realDeadline - q, Math.Abs(q % 5))) / 5, 0);

        QuickSell.Logger.LogDebug($"FindOvertime() returns overtime: {overtime}");
        return overtime;
    }

    protected static string NumberOfItems(int itemCount) => itemCount + (itemCount == 1 ? " item" : " items");

    // Returns the value of objects' prices + how much they cost now (if the buying rate is not 100%)
    protected static string ValueOfItems(List<GrabbableObject> items) =>
        items.Sum(i => i.scrapValue)
        + (
            Mathf.Approximately(StartOfRound.Instance.companyBuyingRate, 1f)
                ? ""
                : $" ({(int)(items.Sum(i => i.scrapValue) * StartOfRound.Instance.companyBuyingRate)})"
        );

    // Returns the value of objects' prices + how much they cost now (if the buying rate is not 100%)
    protected static string ValueOfItems(int totalValue) =>
        totalValue
        + (
            Mathf.Approximately(StartOfRound.Instance.companyBuyingRate, 1f)
                ? ""
                : $" ({(int)(totalValue * StartOfRound.Instance.companyBuyingRate)})"
        );
    
    protected static bool IsPriority(GrabbableObject item) => QuickSell.Instance.PriorityItems.Contains(RemoveClone(item.name), StringComparer.OrdinalIgnoreCase);

    protected static string RemoveClone(string name, string cloneString = "(Clone)") => name.EndsWith(cloneString) ? name[..^cloneString.Length] : name;

    protected static GrabbableObject[] FilterItems(GrabbableObject[] items, bool ignoreBlacklist)
    {
        QuickSell.Logger.LogDebug($"Calling FilterItems({NumberOfItems(items.Count())})");
        if (ignoreBlacklist) QuickSell.Logger.LogDebug($"Ignoring blacklist");
        else QuickSell.Logger.LogDebug($"Blacklisted items: {QuickSell.Instance.ItemBlacklist.Join()}");

        var desk = FindDesk() ?? new DepositItemsDesk() { itemsOnCounter = [] };

        return [.. items
            .Where(i => i is
                {
                    scrapValue: > 0,
                    isHeld: false,
                    isPocketed: false,
                    itemProperties.isScrap: true
                }
                && !desk.itemsOnCounter.Contains(i)
            )
            .Where(i => !QuickSell.Instance.ItemBlacklist.Contains(RemoveClone(i.name), StringComparer.OrdinalIgnoreCase) || ignoreBlacklist)];
    }

    protected static GrabbableObject[] FindItems(GrabbableObject[] items, string itemName)
    {
        QuickSell.Logger.LogDebug($"Calling FindItems({NumberOfItems(items.Count())}, {itemName})");

        var desk = FindDesk() ?? new DepositItemsDesk() { itemsOnCounter = [] };

        return [.. items
            .Where(i => i is
                {
                    isHeld: false,
                    isPocketed: false
                }
                && !desk.itemsOnCounter.Contains(i)
            )
            .Where(i => RemoveClone(i.name).ToLower() == itemName.ToLower())];
    }

    protected internal static DepositItemsDesk? FindDesk()
    {
        QuickSell.Logger.LogDebug("Calling FindDesk0()");
        var desk = Object.FindObjectOfType<DepositItemsDesk>();
        if (GameNetworkManager.Instance == null || desk == null)
        {
            QuickSell.Logger.LogDebug("A desk was not found");
            return null;
        }
        QuickSell.Logger.LogDebug("A desk was found");
        return desk;
    }  // Maybe merge the two functions

    protected internal static bool FindDesk(out DepositItemsDesk desk)
    {
        QuickSell.Logger.LogDebug("Calling FindDesk1()");
        desk = Object.FindObjectOfType<DepositItemsDesk>();
        if (GameNetworkManager.Instance == null || desk == null)
        {
            QuickSell.Logger.LogDebug("A desk was not found");
            sellData.error = "A desk was not found";
            return false;
        }

        QuickSell.Logger.LogDebug("A desk was found");
        return true;
    }
}

public class OvertimeCommand : Command
{
    public override string Name => "Overtime";
    public override string Description => "Shows how much overtime you will get\n" +
        "-n to force non-restart calculations (if you don't know what it is don't use it)";
    public override string[] Commands => [Name.ToLower(), "ot"];
    public override string[] Syntax => ["", "[-n]"];

    public override bool Invoke(string[] args, Dictionary<string, string> kwargs, out string error)
    {
        QuickSell.Logger.LogDebug($"The overtime command was initiated");
        error = "it should not happen";
        int realDeadline = SellCommand.GetDeadline(args.Length > 0 && args[0] == "-n");
        ChatCommandAPI.ChatCommandAPI.Print($"Overtime: {Math.Max((TimeOfDay.Instance.quotaFulfilled + Patches.valueOnDesk + Math.Min(75 * realDeadline - TimeOfDay.Instance.profitQuota, 0)) / 5, 0)}");
        QuickSell.Logger.LogDebug($"Terminating");
        return true;
    }
}