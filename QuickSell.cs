using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using ChatCommandAPI;
using EasyTextEffects.Editor.MyBoxCopy.Extensions;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace QuickSell;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("baer1.ChatCommandAPI")]
public class QuickSell : BaseUnityPlugin  // Add priority help, add ability to write temporary blacklist and priority into the permanent one, update readme file
{
    public static QuickSell Instance { get; private set; } = null!;
    internal static new ManualLogSource Logger { get; private set; } = null!;

    internal ConfigEntry<string> itemBlacklistConfig = null!;
    internal HashSet<string> ItemBlacklistSet = [];
    internal HashSet<string> TempBlacklistAddSet = [];
    internal HashSet<string> TempBlacklistRmSet = [];
    internal HashSet<string> ActiveBlacklistSet = [];
    internal bool UpdateBlacklist = true;

    internal ConfigEntry<string> priorityItemsConfig = null!;
    internal HashSet<string> PriorityItemsSet = [];
    internal HashSet<string> TempPriorityAddSet = [];
    internal HashSet<string> TempPriorityRmSet = [];
    internal HashSet<string> ActivePrioritySet = [];
    internal bool UpdatePriority = true;

    public static List<(string prefabName, string name, string itemName, string scanNodeName)> allItems = [];

    private void Awake()
    {
        Logger = base.Logger;
        Instance = this;

        itemBlacklistConfig = Config.Bind(
            "Items",
            "ItemBlacklist",
            CommaJoin(["ShotgunItem", "KnifeItem", "ZeddogPlushie", "GiftBox"]),
            "Items to never sell by internal name (comma-separated)"
        );
        priorityItemsConfig = Config.Bind(
            "Items",
            "PriorityItems",
            CommaJoin(["Clock", "EasterEgg", "SoccerBall", "WhoopieCushion"]),
            "Items which are prioritized when selling"
        );

        RebuildBlacklistSet();
        itemBlacklistConfig.SettingChanged += (_, _) => RebuildBlacklistSet();

        RebuildPrioritySet();
        priorityItemsConfig.SettingChanged += (_, _) => RebuildPrioritySet();

        _ = new SellCommand();
        _ = new OvertimeCommand();
        var harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);

        harmony.PatchAll();

        Logger.LogInfo($"{MyPluginInfo.PLUGIN_GUID} v{MyPluginInfo.PLUGIN_VERSION} was loaded!");
    }


    public static void OnLobbyEntrance()
    {
        Logger.LogDebug("Calling OnLobbyEntrance()");
        Logger.LogDebug("Creating allItems list");

        // Add all possible items to List<item name, prefab name, scan node name>
        allItems =
        [..
            Resources.FindObjectsOfTypeAll<Item>()
            .Where(i => i.spawnPrefab && "box" != i.itemName)
            .Select(i => (i.spawnPrefab.name, i.name, i.itemName, i.spawnPrefab.GetComponentInChildren<ScanNodeProperties>()?.headerText ?? ""))
        ];

        Logger.LogDebug($"allItems list created. Length: {allItems.Count}");
    }

    public void RebuildBlacklistSet()
    {
        if (!UpdateBlacklist) return;
        QuickSell.Logger.LogDebug($"Constructing a new blacklist");
        QuickSell.Logger.LogDebug($"Old blacklist set: {string.Join(",", ItemBlacklistSet)}");
        ItemBlacklistSet = CommaSplit(itemBlacklistConfig.Value);
        RebuildActiveBlacklist();
        QuickSell.Logger.LogDebug($"New blacklist set: {string.Join(",", ItemBlacklistSet)}");
    }

    public void RebuildActiveBlacklist() =>
        ActiveBlacklistSet = [.. ItemBlacklistSet.Union(TempBlacklistAddSet).Except(TempBlacklistRmSet)];

    public void RebuildPrioritySet()
    {
        if (!UpdatePriority) return;
        QuickSell.Logger.LogDebug($"Constructing a new priority set");
        QuickSell.Logger.LogDebug($"Old priority set: {string.Join(",", PriorityItemsSet)}");
        PriorityItemsSet = CommaSplit(priorityItemsConfig.Value);
        RebuildActivePrioritySet();
        QuickSell.Logger.LogDebug($"New priority set: {string.Join(",", PriorityItemsSet)}");
    }

    public void RebuildActivePrioritySet() =>
        ActivePrioritySet = [.. PriorityItemsSet.Union(TempPriorityAddSet).Except(TempPriorityRmSet)];

    internal static string CommaJoin(HashSet<string> i) => i.Join(delimiter: ",");

    internal static HashSet<string> CommaSplit(string i) => [.. i.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(i => i.Trim())];

    /// <summary>
    /// Prints provided message in chat with start and end lines and a title
    /// </summary>
    /// <param name="message">Provided message</param>
    /// <param name="title">Title displayed on top of the messagee</param>
    /// <param name="addColorToContents">Add colors to the message itself (not the lines) or not</param>
    /// <param name="line">The line in which the title will be embedded and which will be at the end</param>
    /// <param name="color">The colour of this chat block</param>
    public static void FancyChatDisplay(string message, string title = "", bool addColorToContents = true, string line = "===============================", string color = "#00ffff")
    {
        string titleLine;
        if (title != "")
        {
            string titleContents = " " + title + " ";

            // Ensure we have room for at least 2 '=' (one each side) if content is longer than original
            int minimumLength = Math.Max(line.Length, titleContents.Length + 2);

            int equalsNeeded = minimumLength - titleContents.Length;
            int leftEquals = equalsNeeded / 2;  // The smaller value
            int rightEquals = equalsNeeded - leftEquals;  // Extra equal sign goes to the right if odd

            titleLine = $"<color={color}>{new string('=', leftEquals)}{titleContents}{new string('=', rightEquals)}</color>\n";
        }
        else titleLine = $"<color={color}>{line}</color>\n";

        string contents =
            (addColorToContents ? $"<color={color}>{message}</color>" : message) +
            (message.Last() != '\n' ? "\n" : "");

        string endingLine = $"<color={color}>{line}</color>";

        HUDManager.Instance.ChatMessageHistory.Add(titleLine + contents + endingLine);
        UpdateChat();
    }

    public static void UpdateChat()
    {
        HUDManager.Instance.chatText.text = string.Join(
            "\n",
            HUDManager.Instance.ChatMessageHistory
        );
        HUDManager.Instance.PingHUDElement(HUDManager.Instance.Chat, 4f);
    }

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
        public string[] args = [];  // All original arguments given with the command
        public DepositItemsDesk desk = new();  // Desk at the company
        public string variation = "";  // The command variation
        public int value = 0;  // Sum of values of items that should be on the counter
        public string originalValue = "";  // The requested value by the user (after expression evaluation)
        public int quotaLeft = 0;  // Unfullfilled quota
        public int existingMoney = 0;  // The existing money that we need to account for (money in terminal or/and existing overtime)

        // Flags
        public bool e = false;  // Account for existing money
        public bool o = false;  // Check for overtime
        public bool a = false;  // Ignore blacklist
        public bool n = false;  // Force calculations to think that there was no restart before selling even if the client thinks otherwise
        public bool p = false;  // Permanent (blacklist, priority etc.)
    }

    // A wrapper method which runs provided function only if a desk exists
    protected static Action CompleteOnlyWithDesk(Action action) =>
        () =>
        {
            if (FindDesk(out sellData.desk)) action();
        };

    // A dictionary with variations (with added checks for desk existance where needed)
    protected Dictionary<string, Action> actions = new()
    {
        { "help", SellHelp },
        { "blacklist", SellBlacklist },
        { "priority", SellPriority },
        { "", CompleteOnlyWithDesk(SellNoArgs) },
        { "item", CompleteOnlyWithDesk(SellParticularItem) },
        { "quota", CompleteOnlyWithDesk(SellQuota) },
        { "all", CompleteOnlyWithDesk(SellAll) },
        { "amount", CompleteOnlyWithDesk(SellForRequestedAmount) }
    };


    protected static SellData sellData;

    public override bool Invoke(string[] args, Dictionary<string, string> kwargs, out string _)
    {
        QuickSell.Logger.LogDebug("The sell command was initiated");
        _ = "";

        sellData = new() { args = args };

        // Parse the variation and the flags from the arguments
        ParseArguments();

        // Executes chosen variation. Note that after this the sellData.desk variable is assigned if needed
        if (actions.TryGetValue(sellData.variation, out var action)) action();

        return true;
    }

    protected static void ParseArguments()  // Remove -t flag after a few updates
    {
        QuickSell.Logger.LogDebug("Calling ParseArguments()");

        // Parsing flags
        string flags = string.Join("", sellData.args.Where(i => i != "" && i.First() == '-' && i.Length > 1).Select(i => i[1..]));

        // Turn all the flags into variables for readability
        sellData.e = flags.Contains("e") || flags.Contains("t");  // Remove -t after a few updates
        sellData.o = flags.Contains("o");
        sellData.a = flags.Contains("a");
        sellData.n = flags.Contains("n");
        sellData.p = flags.Contains("p");
        if (flags.Contains("t")) ChatCommandAPI.ChatCommandAPI.PrintError("Flag -t as a check for existing money will be depricated soon. Use -e instead");
        QuickSell.Logger.LogDebug($"Flags: -e == {sellData.e}; -o == {sellData.o}; -a == {sellData.a}; -n == {sellData.n}; -p == {sellData.p}");

        sellData.args = [.. sellData.args.Where(i => i != "" && !(i.First() == '-' && i.Length > 1))];

        if (sellData.args.Length == 0) return;

        // Parsing variation
        string probableVariation = sellData.args[0].ToLower();

        if (new string[] { "help" }.Contains(probableVariation)) sellData.variation = "help";
        else if (new string[] { "item", "items", "scrap" }.Contains(probableVariation)) sellData.variation = "item";
        else if (new string[] { "quota" }.Contains(probableVariation)) sellData.variation = "quota";
        else if (new string[] { "all" }.Contains(probableVariation)) sellData.variation = "all";
        else if (new string[] { "blacklist", "bl" }.Contains(probableVariation)) sellData.variation = "blacklist";
        else if (new string[] { "priority", "pr" }.Contains(probableVariation)) sellData.variation = "priority";
        else sellData.variation = "amount";

        QuickSell.Logger.LogDebug($"variation: {(sellData.variation == "amount" ? "no variation keyword was used so assuming <amount>" : sellData.variation)}");

        return;
    }

    protected static void SellNoArgs()
    {
        QuickSell.Logger.LogDebug("Got no arguments -> calling SellNoArgs");

        if (!OpenDoor(out int itemCount, out int totalValue)) return;

        QuickSell.FancyChatDisplay($"Selling {NumberOfItems(itemCount)} with a total value of {ValueOfItems(totalValue)}");
    }

    protected static void SellHelp()
    {
        QuickSell.Logger.LogDebug("variation == \"help\" -> calling SellHelp()");

        // Throw this into another file maybe
        Dictionary<string, string> pages = new()
        {
            {
                "",
                """
                Usage:
                /sell <variation> [flags]

                Command variations:
                "help" to open this page or a specific help page
                "item" to sell all items like the one you are holding or the one you specified
                "quota" to sell exactly for quota
                "all" to sell all unfiltered scrap available
                <amount> (input a number instead of \"amount\") to sell exactly how much you need

                Use "/sell help pages" to see info on all pages
                Use "/sell help flags" to see info on important flags
                Use "/sell help <variation>" to see info on specific command
                """
            },
            {
                "pages",
                """
                Usage:
                /sell help [page]

                Pages:
                default, pages, flags, item, quota, all, amount, -o, -e, -a, -n
                """
            },
            {
                "flags",
                """
                Usage:
                /sell <variation> [flags]

                Combining flags:
                Split: /sell <variation> -e -o -a
                Together: /sell <variation> -eoa

                "-o" to sell accounting for overtime (used with <amount>)
                "-e" for accounting for existing money in terminal and overtime (used with <amount>)
                "-a" to ignore blacklist (used with quota, all, <amount>)
                "-n" to force non-restart overtime calculations (needed in rare edge cases)

                Use "/sell help <flag>" to see info on specific flag
                """
            },
            {
                "item",
                """
                Usage:
                /sell item [item]

                Sells all items with the specified name. If no name was specified then checks what item you are holding and gets it's name instead (and sells this held item too)
                """
            },
            {
                "quota",
                """
                Usage:
                /sell quota [-a]

                Checks how much quota is left and tries to sell exactly that (if it's not enough, nothing will be sold and if exact value isn't achievable sells the smallest value after that)
                """
            },
            {
                "all",
                """
                Usage:
                /sell all [-a]

                Sells all (non-blacklisted, use -a to ignore blacklist) items
                """
            },
            {
                "amount",
                """
                Usage:
                /sell <amount> [-o] [-e] [-a] [-n]

                Tries to sell exactly how much you specified. If there is not enough scrap, sells nothing. If an exact value isn't achievable sells the smallest value after that
                """
            },
            {
                "blacklist",
                """
                Usage:
                /sell bl [-a] [-p]
                /sell bl {add | ad | a | +} [itemName] [-p]
                /sell bl {remove | rm | r | -} [itemName] [-p]
                /sell bl {empty | flash | flush}
                
                Without modifiers just prints an active blacklist, you can add -a to also display temporary blacklist or -p to display permanent blacklist instead.
                By using "/sell bl +" ("/sell bl -") you can temporarily blacklist (or prohibit to blacklist) an item currently in your hands. You can also add/remove it from a permanent blacklist by using -p flag.
                By using "/sell bl empty" you can clear temporary blacklist in case you don't need it anymore (keep in mind that it automatically resets when you close the game window)
                """
            },
            {
                "priority",
                """
                Usage:
                /sell pr [-a] [-p]
                /sell pr {add | ad | a | +} [itemName] [-p]
                /sell pr {remove | rm | r | -} [itemName] [-p]
                /sell pr {empty | flash | flush}
                
                Without modifiers just prints an active priority set, you can add -a to also display temporary priority set or -p to display permanent priority set instead.
                By using "/sell pr +" ("/sell bl -") you can temporarily prioritize (or prohibit form being prioritized) an item currently in your hands. You can also add/remove it from a permanent priority set by using -p flag.
                By using "/sell pr empty" you can clear temporary priority set in case you don't need it anymore (keep in mind that it automatically resets when you close the game window)
                """
            },
            {
                "-o",
                """
                Usage:
                /sell <amount> -o

                Respects the fact that your sold items can cause overtime and includes it in the calculations (note that the overtime caused by already sold items isn't included, you need -e flag for that) so that: requested value = final value in terminal (after leaving the planet) - existing money (look into -e help page for that)
                """
            },
            {
                "-e",
                """
                Usage:
                /sell <amount> -e

                (Previously -t, but was changed to -e)
                Removes existing money (already existing credits in terminal, items on desk and, if -o flag is present, future overtime based on these two) from your requsted value so that: requested value = final value in terminal (after leaving the planet) = existing money + sold items (+ overtime caused by sold items if -o flag is present)
                """
            },
            {
                "-a",
                """
                Usage:
                /sell {quota | all | amount | bl | pr} -a

                When trying to find right items to sell, ignores all blacklists so that *EVERY* item can be sold. If used with "/sell bl" or "/sell pr" displays both temporary blacklists (or priority sets) along with the active one
                """
            },
            {
                "-p",
                """
                Usage:
                /sell {bl | pr} [+ | -] -p

                When using the blacklist (or priority) command can be used to affect permanent blacklist (or priority set) instead of the temporary one
                """
            },
            {
                "-n",
                """
                Usage:
                /sell <amount> -n

                Forces EVERY overtime calculation that occures during the execution of THIS command to think that there was no rehost after the final day of this quota, even if there was one). It is only needed if a host has a mod for late joining (aka LateCompany) and you joined after the final day of this quota (your client will think that there was a rehost then). There is no way (that I know of, at least, if you know one please tell me) to check if there was or wasn't a real rehost in this case, and if there wasn't, then all overtime calculations will be 15 smaller. This flag accounts for that, but note that if the rehost has actually occured and you used this flag then all overtime calculation will be 15 bigger so you should ask your host if they have done a rehost or not to get it right
                """
            }
        };

        if (sellData.args.Length <= 1 && !(sellData.o || sellData.e || sellData.a || sellData.n))
        {
            QuickSell.FancyChatDisplay(pages[""], "HELP PAGE");
            return;
        }

        // Switch for flags
        switch (true)
        {
            case bool when sellData.o:
                QuickSell.FancyChatDisplay(pages["-o"], "-O HELP PAGE");
                return;
            case bool when sellData.e:
                QuickSell.FancyChatDisplay(pages["-e"], "-E HELP PAGE");
                return;
            case bool when sellData.a:
                QuickSell.FancyChatDisplay(pages["-a"], "-A HELP PAGE");
                return;
            case bool when sellData.p:
                QuickSell.FancyChatDisplay(pages["-p"], "-P HELP PAGE");
                return;
            case bool when sellData.n:
                QuickSell.FancyChatDisplay(pages["-n"], "-N HELP PAGE");
                return;
        }

        // Switch for everything else
        switch (sellData.args[1].ToLower())
        {
            case "pages":
            case "page":
            case "help":
                QuickSell.FancyChatDisplay(pages["pages"], "PAGES HELP PAGE");
                return;
            case "flags":
            case "flag":
                QuickSell.FancyChatDisplay(pages["flags"], "FLAG HELP PAGE");
                return;
            case "item":
            case "items":
                QuickSell.FancyChatDisplay(pages["item"], "ITEM HELP PAGE");
                return;
            case "quota":
                QuickSell.FancyChatDisplay(pages["quota"], "QUOTA HELP PAGE");
                return;
            case "all":
                QuickSell.FancyChatDisplay(pages["all"], "ALL HELP PAGE");
                return;
            case "amount":
            case "<amount>":
                QuickSell.FancyChatDisplay(pages["amount"], "AMOUNT HELP PAGE");
                return;
            case "blacklist":
                QuickSell.FancyChatDisplay(pages["blacklist"], "BLACKLIST HELP PAGE");
                return;
            case "priority":
                QuickSell.FancyChatDisplay(pages["priority"], "PRIORITY HELP PAGE");
                return;
        }

        ChatCommandAPI.ChatCommandAPI.PrintError("No page with this name exists");
    }

    protected static void SellParticularItem()
    {
        QuickSell.Logger.LogDebug("variation == \"item\" -> calling SellParticularItem()");

        string itemName;
        if (sellData.args.Length <= 1)
        {
            if (!CheckHeldItem(out itemName))
            QuickSell.Logger.LogDebug($"Item to sell: {itemName}");

            QuickSell.Logger.LogDebug("Dropping held item");
            StartOfRound.Instance.localPlayerController.DiscardHeldObject();
        }
        else
        {
            itemName = GetActualItemByName(sellData.args[1]).prefabName;
            QuickSell.Logger.LogDebug($"Item to sell: {itemName}");
        }

        var items = Object.FindObjectsOfType<GrabbableObject>();
        if (items == null || items.Length == 0)
        {
            QuickSell.Logger.LogDebug("No items were found");
            ChatCommandAPI.ChatCommandAPI.PrintError("No items were found");
            return;
        }

        items = FindItems(items, itemName);
        if (items == null || items.Length == 0)
        {
            QuickSell.Logger.LogDebug($"No items called \"{itemName}\" were detected");
            ChatCommandAPI.ChatCommandAPI.PrintError($"No items called \"{itemName}\" were detected");
            return;
        }

        if (!SellItems([.. items], out int itemCount)) return;

        if (!sellData.desk.doorOpen)
        {
            QuickSell.Logger.LogDebug("The door is not open -> opening it");
            sellData.desk.SetTimesHeardNoiseServerRpc(5f);
        }

        QuickSell.FancyChatDisplay($"Selling {NumberOfItems(itemCount)} named \"{itemName}\" with a total value of {ValueOfItems([.. items])}");

        QuickSell.Logger.LogDebug("The sell command completed it's job, terminating");
    }

    protected static void SellQuota()
    {
        QuickSell.Logger.LogDebug("variation == \"quota\" -> calling SellQuota()");

        sellData.value = TimeOfDay.Instance.profitQuota - (TimeOfDay.Instance.quotaFulfilled + Patches.valueOnDesk);
        sellData.originalValue = sellData.value.ToString();
        QuickSell.Logger.LogDebug($"The requested value (profitQuota - (quotaFulfilled + value on desk)): {sellData.value}");

        if (sellData.value < 1)
        {
            QuickSell.FancyChatDisplay($"Quota is already fulfilled");
            QuickSell.Logger.LogDebug("Quota is already fulfilled -> nothing left to do");
            return;
        }

        SellForValue();
    }

    protected static void SellAll()
    {
        QuickSell.Logger.LogDebug("variation == \"all\" -> calling SellAll()");
        sellData.value = -1;

        SellForValue();
    }

    protected static void SellForRequestedAmount()
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
            ChatCommandAPI.ChatCommandAPI.PrintError("Failed to evalute expression");
            return;
        }

        // Two checks for value being right
        if (!int.TryParse(evaluatedExpression, out sellData.value) || sellData.value < 0)
        {
            QuickSell.Logger.LogDebug("The value is not convertable into integer");
            ChatCommandAPI.ChatCommandAPI.PrintError("The value is not convertable into integer");
            return;
        }
        if (sellData.value < 0)
        {
            QuickSell.Logger.LogDebug("The value must be positive");
            ChatCommandAPI.ChatCommandAPI.PrintError("The value must be positive");
            return;
        }
        QuickSell.Logger.LogDebug($"Expression evaluated: {expression} => {evaluatedExpression}");

        // Assigning the result of the expression as the requested value
        sellData.originalValue = sellData.value.ToString();

        // Logic for accounting for money in terminal and already existing overtime
        if (sellData.e)
        {
            QuickSell.Logger.LogDebug($"Entering logic for accounting for existing money");

            var terminal = UnityEngine.Object.FindObjectOfType<Terminal>();
            if (terminal == null)
            {
                ChatCommandAPI.ChatCommandAPI.PrintError("Cannot find terminal!");
                QuickSell.Logger.LogDebug($"Cannot find terminal!");
                return;
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
                return;
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

        SellForValue();
    }

    protected static void SellBlacklist()
    {
        QuickSell.Logger.LogDebug($"variation == \"blacklist\" -> calling SellBlacklist()");

        if (sellData.args.Length <= 1 && sellData.p)
        {
            ItemDisplay(QuickSell.Instance.ItemBlacklistSet, "PERMANENT BLACKLIST");
            return;
        }
        else if (sellData.args.Length <= 1)
        {
            if (sellData.a)
            {
                ItemDisplay(QuickSell.Instance.TempBlacklistRmSet, "TEMPORARY UNBLACKLISTED");
                ItemDisplay(QuickSell.Instance.TempBlacklistAddSet, "TEMPORARY BLACKLIST");
            }

            ItemDisplay(QuickSell.Instance.ActiveBlacklistSet, "ACTIVE BLACKLIST");
            return;
        }

        // Checking if we should add or remove from the config
        bool add;
        if (new string[] { "add", "ad", "a", "+" }.Contains(sellData.args[1])) add = true;
        else if (new string[] { "remove", "rm", "r", "-" }.Contains(sellData.args[1])) add = false;
        else if (new string[] { "empty", "flash", "flush"}.Contains(sellData.args[1]))
        {
            if (sellData.p)
            {
                QuickSell.Logger.LogDebug($"Denied request to empty permanent blacklist");
                ChatCommandAPI.ChatCommandAPI.PrintError($"The permanent blacklist cannot be emptied by the mod itself for safety reasons. If you really want to do it use something like LethalConfig or R2Modman config editor");
                return;
            }

            QuickSell.Instance.TempBlacklistAddSet.Clear();
            QuickSell.Instance.TempBlacklistRmSet.Clear();
            QuickSell.Instance.RebuildActiveBlacklist();
            QuickSell.FancyChatDisplay("Successfully emptied temporary blacklist");
            return;
        }
        else
        {
            QuickSell.Logger.LogDebug($"Wrong arguments. If you don't know how to use the command use \"/sell help blacklist\"");
            ChatCommandAPI.ChatCommandAPI.PrintError($"Wrong arguments. If you don't know how to use the command use \"/sell help blacklist\"");
            return;
        }

        // Checking what item name to use
        string actualItemName;
        if (sellData.args.Length <= 2)
        {
            if (!CheckHeldItem(out actualItemName)) return;
        }
        else
        {
            actualItemName = GetActualItemByName(sellData.args[2]).prefabName;
        }
        QuickSell.Logger.LogDebug($"Item to blacklist: {actualItemName}");

        if (sellData.p) ChangePermanentBlacklist(actualItemName, add);
        else ChangeTemporaryBlacklist(actualItemName, add);
    }

    protected static void ChangePermanentBlacklist(string actualItemName, bool add)
    {
        if (actualItemName == "")
        {
            QuickSell.Logger.LogDebug($"Wrong item name");
            ChatCommandAPI.ChatCommandAPI.PrintError($"Wrong item name");
            return;
        }

        if (add && QuickSell.Instance.ItemBlacklistSet.Contains(actualItemName))
        {
            QuickSell.Logger.LogDebug($"\"{actualItemName}\" is already in the permanent blacklist");
            ChatCommandAPI.ChatCommandAPI.PrintWarning($"\"{actualItemName}\" is already in the permanent blacklist");
            return;
        }
        else if (add)
        {
            QuickSell.Instance.ItemBlacklistSet.Add(actualItemName);

            QuickSell.FancyChatDisplay($"Successfully permanently blacklisted \"{actualItemName}\"");
        }
        else if (!QuickSell.Instance.ItemBlacklistSet.Contains(actualItemName))
        {
            QuickSell.Logger.LogDebug($"\"{actualItemName}\" is not in the permanent blacklist");
            ChatCommandAPI.ChatCommandAPI.PrintWarning($"\"{actualItemName}\" is not in the permanent blacklist");
            return;
        }
        else
        {
            QuickSell.Instance.ItemBlacklistSet.Remove(actualItemName);

            QuickSell.FancyChatDisplay($"Successfully removed \"{actualItemName}\" from the permanent blacklist");
        }

        QuickSell.Logger.LogDebug($"Config before: {QuickSell.Instance.itemBlacklistConfig.Value}");
        QuickSell.Instance.UpdateBlacklist = false;
        QuickSell.Instance.itemBlacklistConfig.Value = QuickSell.CommaJoin(QuickSell.Instance.ItemBlacklistSet);
        QuickSell.Instance.UpdateBlacklist = true;
        QuickSell.Logger.LogDebug($"Config after: {QuickSell.Instance.itemBlacklistConfig.Value}");

        QuickSell.Instance.RebuildActiveBlacklist();
    }

    protected static void ChangeTemporaryBlacklist(string actualItemName, bool add)
    {
        if (actualItemName == "")
        {
            QuickSell.Logger.LogDebug($"Wrong item name");
            ChatCommandAPI.ChatCommandAPI.PrintError($"Wrong item name");
            return;
        }

        if (add && QuickSell.Instance.TempBlacklistAddSet.Contains(actualItemName))
        {
            QuickSell.Logger.LogDebug($"\"{actualItemName}\" is already temporarily blacklisted");
            ChatCommandAPI.ChatCommandAPI.PrintWarning($"\"{actualItemName}\" is already temporarily blacklisted");
            return;
        }
        else if (add)
        {
            QuickSell.Instance.TempBlacklistRmSet.Remove(actualItemName);
            QuickSell.Instance.TempBlacklistAddSet.Add(actualItemName);

            QuickSell.FancyChatDisplay($"Successfully temporarily blacklisted \"{actualItemName}\"");
        }
        else if (QuickSell.Instance.TempBlacklistRmSet.Contains(actualItemName))
        {
            QuickSell.Logger.LogDebug($"\"{actualItemName}\" is already temporarily prohibited to blacklist");
            ChatCommandAPI.ChatCommandAPI.PrintWarning($"\"{actualItemName}\" is already temporarily prohibited to blacklist");
            return;
        }
        else
        {
            QuickSell.Instance.TempBlacklistRmSet.Add(actualItemName);
            QuickSell.Instance.TempBlacklistAddSet.Remove(actualItemName);

            QuickSell.FancyChatDisplay($"Successfully temporarily prohibited to blacklist \"{actualItemName}\"");
        }

        QuickSell.Instance.RebuildActiveBlacklist();
    }

    protected static void SellPriority()
    {
        QuickSell.Logger.LogDebug($"variation == \"priority\" -> calling SellPriority()");

        if (sellData.args.Length <= 1 && sellData.p)
        {
            ItemDisplay(QuickSell.Instance.PriorityItemsSet, "PERMANENT PRIORITY SET");
            return;
        }
        else if (sellData.args.Length <= 1)
        {
            if (sellData.a)
            {
                ItemDisplay(QuickSell.Instance.TempPriorityRmSet, "TEMPORARY UNPRIORITIZED");
                ItemDisplay(QuickSell.Instance.TempPriorityAddSet, "TEMPORARY PRIORITY SET");
            }

            ItemDisplay(QuickSell.Instance.ActivePrioritySet, "ACTIVE PRIORITY SET");
            return;
        }

        // Checking if we should add or remove from the config
        bool add;
        if (new string[] { "add", "ad", "a", "+" }.Contains(sellData.args[1])) add = true;
        else if (new string[] { "remove", "rm", "r", "-" }.Contains(sellData.args[1])) add = false;
        else if (new string[] { "empty", "flash", "flush" }.Contains(sellData.args[1]))
        {
            if (sellData.p)
            {
                QuickSell.Logger.LogDebug($"Denied request to empty permanent priority set");
                ChatCommandAPI.ChatCommandAPI.PrintError($"The permanent priority set cannot be emptied by the mod itself for safety reasons. If you really want to do it use something like LethalConfig or R2Modman config editor");
                return;
            }

            QuickSell.Instance.TempPriorityAddSet.Clear();
            QuickSell.Instance.TempPriorityRmSet.Clear();
            QuickSell.Instance.RebuildActivePrioritySet();
            QuickSell.FancyChatDisplay("Successfully emptied temporary priority set");
            return;
        }
        else
        {
            QuickSell.Logger.LogDebug($"Wrong arguments. If you don't know how to use the command use \"/sell help priority\"");
            ChatCommandAPI.ChatCommandAPI.PrintError($"Wrong arguments. If you don't know how to use the command use \"/sell help priority\"");
            return;
        }

        // Checking what item name to use
        string actualItemName;
        if (sellData.args.Length <= 2)
        {
            if (!CheckHeldItem(out actualItemName)) return;
        }
        else
        {
            actualItemName = GetActualItemByName(sellData.args[2]).prefabName;
        }
        QuickSell.Logger.LogDebug($"Item: {actualItemName}");

        if (sellData.p) ChangePermanentPrioritySet(actualItemName, add);
        else ChangeTemporaryPrioritySet(actualItemName, add);
    }

    protected static void ChangePermanentPrioritySet(string actualItemName, bool add)
    {
        if (actualItemName == "")
        {
            QuickSell.Logger.LogDebug($"Wrong item name");
            ChatCommandAPI.ChatCommandAPI.PrintError($"Wrong item name");
            return;
        }

        if (add && QuickSell.Instance.PriorityItemsSet.Contains(actualItemName))
        {
            QuickSell.Logger.LogDebug($"\"{actualItemName}\" is already in the permanent priority set");
            ChatCommandAPI.ChatCommandAPI.PrintWarning($"\"{actualItemName}\" is already in the permanent priority set");
            return;
        }
        else if (add)
        {
            QuickSell.Instance.PriorityItemsSet.Add(actualItemName);

            QuickSell.FancyChatDisplay($"Successfully added \"{actualItemName}\" to the permanent priority set");
        }
        else if (!QuickSell.Instance.PriorityItemsSet.Contains(actualItemName))
        {
            QuickSell.Logger.LogDebug($"\"{actualItemName}\" is not in the permanent priority set");
            ChatCommandAPI.ChatCommandAPI.PrintWarning($"\"{actualItemName}\" is not in the permanent priority set");
            return;
        }
        else
        {
            QuickSell.Instance.PriorityItemsSet.Remove(actualItemName);

            QuickSell.FancyChatDisplay($"Successfully removed \"{actualItemName}\" from the permanent priority set");
        }

        QuickSell.Logger.LogDebug($"Config before: {QuickSell.Instance.priorityItemsConfig.Value}");
        QuickSell.Instance.UpdatePriority = false;
        QuickSell.Instance.priorityItemsConfig.Value = QuickSell.CommaJoin(QuickSell.Instance.PriorityItemsSet);
        QuickSell.Instance.UpdatePriority = true;
        QuickSell.Logger.LogDebug($"Config after: {QuickSell.Instance.priorityItemsConfig.Value}");

        QuickSell.Instance.RebuildActivePrioritySet();
    }

    protected static void ChangeTemporaryPrioritySet(string actualItemName, bool add)
    {
        if (actualItemName == "")
        {
            QuickSell.Logger.LogDebug($"Wrong item name");
            ChatCommandAPI.ChatCommandAPI.PrintError($"Wrong item name");
            return;
        }

        if (add && QuickSell.Instance.TempPriorityAddSet.Contains(actualItemName))
        {
            QuickSell.Logger.LogDebug($"\"{actualItemName}\" is already in the temporarily priority set");
            ChatCommandAPI.ChatCommandAPI.PrintWarning($"\"{actualItemName}\" is already in the temporarily priority set");
            return;
        }
        else if (add)
        {
            QuickSell.Instance.TempPriorityRmSet.Remove(actualItemName);
            QuickSell.Instance.TempPriorityAddSet.Add(actualItemName);

            QuickSell.FancyChatDisplay($"Successfully added \"{actualItemName}\" to the temporary priority set");
        }
        else if (QuickSell.Instance.TempPriorityRmSet.Contains(actualItemName))
        {
            QuickSell.Logger.LogDebug($"\"{actualItemName}\" is already temporarily prohibited to prioritize");
            ChatCommandAPI.ChatCommandAPI.PrintWarning($"\"{actualItemName}\" is already temporarily prohibited to prioritize");
            return;
        }
        else
        {
            QuickSell.Instance.TempPriorityRmSet.Add(actualItemName);
            QuickSell.Instance.TempPriorityAddSet.Remove(actualItemName);

            QuickSell.FancyChatDisplay($"Successfully temporarily prohibited to prioritize \"{actualItemName}\"");
        }

        QuickSell.Instance.RebuildActivePrioritySet();
    }

    /// <summary>
    /// Displays a list of item ```names``` in chat
    /// </summary>
    /// <param name="items"></param>
    /// <param name="title"></param>
    /// <param name="normalColor"></param>
    /// <param name="errorColor"></param>
    /// <param name="noNameText"></param>
    protected static void ItemDisplay(IEnumerable<string> items, string title, string normalColor = "#00ffff", string errorColor = "#ff0000", string noNameText = "MISSING NAME")
    {
        if (items.IsNullOrEmpty()) return;

        string itemName;
        string printout = "";
        foreach (string name in items)
        {
            itemName = GetActualItemByName(name).itemName;
            if (itemName != "") printout += $"<color={normalColor}>{itemName} ({name})</color>\n";
            else printout += $"<color={errorColor}>{noNameText} ({name})</color>\n";
        }

        QuickSell.FancyChatDisplay(printout, title, false);
    }

    /// <summary>
    /// Checks if the item provided exists in the allItems list with any of the names and spits out a prefab of it
    /// </summary>
    /// <param name="itemName"></param>
    /// <returns></returns>
    protected static (string prefabName, string name, string itemName, string scanNodeName)
    GetActualItemByName(string itemName) =>
        QuickSell.allItems
            .Where(i =>
                new[] { i.prefabName, i.name, i.itemName, i.scanNodeName }
                    .Any(j => j != null && j.Equals(itemName, StringComparison.OrdinalIgnoreCase)))
            .DefaultIfEmpty(("", "", "", ""))
            .First();

    /// <summary>
    /// Checks if holding an item and gives its name with itemName
    /// </summary>
    /// <param name="itemName">The name of a held item</param>
    /// <returns>true if an item is being held and false otherwise</returns>
    protected static bool CheckHeldItem(out string itemName)
    {
        itemName = "";

        var player = StartOfRound.Instance.localPlayerController;

        if (player == null)
        {
            QuickSell.Logger.LogDebug("localPlayerController == null -> returning false");
            ChatCommandAPI.ChatCommandAPI.PrintError("localPlayerController == null");
            return false;
        }

        var heldItem = player.ItemSlots[player.currentItemSlot];

        if (heldItem == null || heldItem.name == "")
        {
            QuickSell.Logger.LogDebug("No item is held and no item was specified");
            ChatCommandAPI.ChatCommandAPI.PrintError("No item is held and no item was specified");
            return false;
        }

        itemName = RemoveClone(heldItem.name);
        return true;
    }

    // Unites the whole (before, while and after) sell process (if there is a resulting value which we need to get) itself after the needed value has been found
    protected static async void SellForValue()  // Change calculated overtime so it uses actual sold value instead of requested
    {
        QuickSell.Logger.LogDebug($"Calling SellForValue({sellData.value})");

        var items = await Task.Run(() => ItemsForValue(sellData.value, sellData.a));
        if (items == null)
        {
            QuickSell.Logger.LogDebug("Got null from ItemsForValue() -> not selling anything");
            ChatCommandAPI.ChatCommandAPI.PrintError("No items were found");
            return;
        }

        if (items.Count == 0)
        {
            QuickSell.Logger.LogDebug("The list of items you need to sell is empty so you probably can't afford the amount you requested. If not, please report this.");
            ChatCommandAPI.ChatCommandAPI.PrintError("You can't afford to sell that amount");
            return;
        }

        if (!SellItems(items, out int itemCount))
        {
            QuickSell.Logger.LogDebug("Error selling items");
            ChatCommandAPI.ChatCommandAPI.PrintError("Error selling items");
            return;
        }

        if (!sellData.desk.doorOpen)
        {
            QuickSell.Logger.LogDebug("The door is not open -> opening it");
            sellData.desk.SetTimesHeardNoiseServerRpc(5f);
        }
        else
        {
            QuickSell.Logger.LogDebug("The door is open -> starting coroutine to check later if the items are still on desk");
            StartOfRound.Instance.StartCoroutine(DelayedDeskCheck());
        }

        // Calculates overtime addition caused by this sell command
        int calculatedOvertime = sellData.o || sellData.variation == "all" ? FindOvertime(items.Sum(obj => obj.scrapValue), sellData.quotaLeft, sellData.n) : 0;

        // The printout of the selling results
        QuickSell.FancyChatDisplay(
            $"Selling {NumberOfItems(itemCount)} with a total value of {ValueOfItems(items)}" +

            $"{(
                calculatedOvertime != 0
                ? $" + {calculatedOvertime} overtime"
                : ""
            )}" +

            $"{(
                sellData.e
                ? $" + {sellData.existingMoney} existing money"
                : ""
            )}" +

            $"{(
                sellData.originalValue != ""
                ? $":\n{items.Sum(obj => obj.scrapValue) + calculatedOvertime + sellData.existingMoney} sold / {sellData.originalValue} requested"
                : $", sold every {(sellData.a ? "" : "unfiltered ")}item"
            )}"
        );

        QuickSell.Logger.LogDebug("The sell command completed it's job, terminating");
    }

    protected static List<GrabbableObject>? ItemsForValue(int value, bool ignoreBlacklist)
    {
        QuickSell.Logger.LogDebug($"Calling ItemsForValue({value})");

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

        QuickSell.Logger.LogDebug($"Items with priority: {QuickSell.Instance.ActivePrioritySet.Join(delimiter: ", ")}");
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

        QuickSell.Logger.LogDebug($"Looping through every item");
        foreach (var i in items)
        {
            if (i == null)
                continue;
            QuickSell.Logger.LogDebug($"Item: {i.name}, price: {i.scrapValue}");
            itemCount++;

            var vector = RoundManager.RandomPointInBounds(sellData.desk.triggerCollider.bounds);
            vector.y = sellData.desk.triggerCollider.bounds.min.y;
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
            vector = sellData.desk.deskObjectsContainer.transform.InverseTransformPoint(vector);

            sellData.desk.AddObjectToDeskServerRpc(i.NetworkObject);
            GameNetworkManager.Instance.localPlayerController.PlaceGrabbableObject(
                sellData.desk.deskObjectsContainer.transform,
                vector,
                false,
                i
            );
            GameNetworkManager.Instance.localPlayerController.PlaceObjectServerRpc(
                i.NetworkObject,
                sellData.desk.deskObjectsContainer,
                vector,
                false
            );
        }


        QuickSell.Logger.LogDebug($"Selling completed");
        return true;
    }

    protected static bool OpenDoor(out int itemCount, out int totalValue)
    {
        QuickSell.Logger.LogDebug("Calling OpenDoor()");

        itemCount = 0;
        totalValue = 0;

        itemCount = sellData.desk.itemsOnCounter.Count;
        totalValue = sellData.desk.itemsOnCounter.Sum(i => i.scrapValue);
        QuickSell.Logger.LogDebug($"There are {NumberOfItems(itemCount)} on the desk worth {ValueOfItems(totalValue)}");
        if (itemCount == 0)
        {
            QuickSell.Logger.LogDebug("No items on the desk");
            ChatCommandAPI.ChatCommandAPI.PrintError("No items on the desk");
            return false;
        }
        
        if (sellData.desk.doorOpen)
        {
            QuickSell.Logger.LogDebug("Door was already open -> nothing left to do");
            ChatCommandAPI.ChatCommandAPI.PrintError("Door already open");
            return false;
        }

        QuickSell.Logger.LogDebug("Opening a door");
        sellData.desk.SetTimesHeardNoiseServerRpc(5f);
        return true;
    }

    public static IEnumerator DelayedDeskCheck()
    {
        QuickSell.Logger.LogDebug("Calling DelayedDeskCheck() (DDS)");

        yield return new WaitForSeconds(10f);
        QuickSell.Logger.LogDebug("DDS: 10 seconds passed");

        if (sellData.desk.itemsOnCounter.Count <= 0)
        {
            QuickSell.Logger.LogDebug("DDS: Desk still has no items, terminating");
            yield break;
        }
        QuickSell.Logger.LogDebug("DDS: Desk still has items");

        if (sellData.desk.doorOpen)
        {
            QuickSell.Logger.LogDebug("Door was already open, terminating");
            yield break;
        }

        QuickSell.Logger.LogDebug("Opening a door");
        sellData.desk.SetTimesHeardNoiseServerRpc(5f);
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
    
    protected static bool IsPriority(GrabbableObject item) => QuickSell.Instance.ActivePrioritySet.Contains(RemoveClone(item.name), StringComparer.OrdinalIgnoreCase);

    protected static string RemoveClone(string name, string cloneString = "(Clone)") => name.EndsWith(cloneString) ? name[..^cloneString.Length] : name;

    protected static GrabbableObject[] FilterItems(GrabbableObject[] items, bool ignoreBlacklist)
    {
        QuickSell.Logger.LogDebug($"Calling FilterItems({NumberOfItems(items.Count())})");
        if (ignoreBlacklist) QuickSell.Logger.LogDebug($"Ignoring blacklist");
        else QuickSell.Logger.LogDebug($"Blacklisted items: {QuickSell.Instance.ActiveBlacklistSet.Join()}");

        return [.. items
            .Where(i => i is
                {
                    scrapValue: > 0,
                    isHeld: false,
                    isPocketed: false,
                    itemProperties.isScrap: true
                }
                && !sellData.desk.itemsOnCounter.Contains(i)
            )
            .Where(i => !QuickSell.Instance.ActiveBlacklistSet.Contains(RemoveClone(i.name), StringComparer.OrdinalIgnoreCase) || ignoreBlacklist)];
    }

    protected static GrabbableObject[] FindItems(GrabbableObject[] items, string itemName)
    {
        QuickSell.Logger.LogDebug($"Calling FindItems({NumberOfItems(items.Count())}, {itemName})");

        return [.. items
            .Where(i => i is
                {
                    isHeld: false,
                    isPocketed: false
                }
                && !sellData.desk.itemsOnCounter.Contains(i)
            )
            .Where(i => RemoveClone(i.name).ToLower() == itemName.ToLower())];
    }

    protected internal static bool FindDesk(out DepositItemsDesk desk)
    {
        QuickSell.Logger.LogDebug("Calling FindDesk1()");
        desk = Object.FindObjectOfType<DepositItemsDesk>();
        if (GameNetworkManager.Instance == null || desk == null)
        {
            QuickSell.Logger.LogDebug("A desk was not found");
            ChatCommandAPI.ChatCommandAPI.PrintError("A desk was not found");
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
        QuickSell.FancyChatDisplay($"Overtime: {Math.Max((TimeOfDay.Instance.quotaFulfilled + Patches.valueOnDesk + Math.Min(75 * realDeadline - TimeOfDay.Instance.profitQuota, 0)) / 5, 0)}");

        ChatCommandAPI.ChatCommandAPI.Print(QuickSell.allItems.Select(i => $"{i.prefabName} : {i.name} : {i.itemName} : {i.scanNodeName}").Join(delimiter: "\n"));
        ChatCommandAPI.ChatCommandAPI.Print(args.Join(delimiter: "\n"));


        QuickSell.Logger.LogDebug($"Terminating");
        return true;
    }
}