# QuickSell

## Description:

Allows you to sell scrap on company using chat commands. Allows you to (temporarily or permanently) blacklist items and prioritise them when selling. Works on modded company-like moons too!

Only YOU need this mod in order for it to work


## TL;DR:

<details open>
  <summary>Click to collapse/expand</summary>

### Selling

- If you need an `<amount>` of money overall (after overtime) use `/sell <amount> -eo`

- If you need to have `<amount>` more than you already have (after overtime): use `/sell <amount> -o`

- If you just wanna sell some items worth `<amount>` use `/sell <amount>`

- If you wanna sell exactly to quota use `/sell quota`

- If you wanna sell all items on ship use `/sell all`

- If you wanna sell one TYPE of item, hold this item in your hands and use `/sell item`

- If you wanna see current overtime use `/ot`

### Blacklist

- If you wanna add (remove) an item to blacklist config (permanently) use `/sell bl + -p` (`/sell bl - -p`) while holding the item in your hands

- If you wanna add (remove) an item to temporary config (empties when you close the game) use `/sell bl +` (`/sell bl -`) while holding the item in your hands

- If you wanna empty the temporary blacklist use `/sell bl empty`

- If you wanna display a blacklist use `/sell bl [-a] [-p]`

### Priority

- If you wanna add (remove) an item to priority config permanently use `/sell pr + -p` (`/sell pr - -p`) while holding the item in your hands

- If you wanna add (remove) an item to temporary config (empties when you close the game) use `/sell pr +` (`/sell pr -`) while holding the item in your hands

- If you wanna empty the temporary blacklist use `/sell pr empty`

- If you wanna display a blacklist use `/sell pr [-a] [-p]`

</details>


## Usage:

<details open>
  <summary>Click to collapse/expand</summary>

There is specific information on each command and its usage in the in-game documentation (help pages)

- `/sell help [page]` to open general or specific help page

- `/sell help pages` to open a list of all help pages

- `/sell item [item]` to sell all items like the one you are holding or the one you specified

- `/sell quota [-a]` to sell exactly for quota

- `/sell all [-a]` to sell all unfiltered scrap available

- `/sell <amount> [-o] [-e] [-a] [-n]` to sell exactly how much you need

- `/sell {bl | pr} [-a] [-p]` to show a blacklist/priority set (without flags prints active blacklist/priority set)

- `/sell {bl | pr} {+ | -} [-p]` to add/remove an item to/from the temprory (or permanent with -p flag) blacklist/priority set

- `/sell {bl | pr} empty` to empty the temporary blacklist/priority set

- `/ot [-n]` to see overtime

</details>


## Flags:

<details open>
  <summary>Click to collapse/expand</summary>

#### Again there are help pages dedicated to each flag and to flags in general

- `-o` `(overtime)` to sell accounting for overtime (used with <amount> and often goes together with -e)

- `-e` `(existing)` for accounting for existing money in terminal and overtime (used with <amount> and almost always goes together with -o)

- `-a` `(all)` to ignore blacklist (used with quota, all, <amount>)

- `-p` `(permanent)` when using `/sell bl` allows you to affect permanent blacklist instead of the temporary one

- `-n` `(nonrestart)` to FORSE non-restart overtime calculations (needed in very rare edge cases with late joining mods after late joining as a client)

</details>


## In-depth explanation

### Features

<details>
  <summary>Click to collapse/expand</summary>

#### Blacklist

The blacklist tells the mod which items not to sell. There are three (four) kinds of it: permanent, temporary(add), temporary(remove) and active.
- Permanent blacklist loads itself from the config at the start of the game. Although it's possible, I wouldn't recommend to modify the config by yourself, instead modify the permanent blacklist through in-game commands explained later (to avoid any user-made errors).
- Temporary blacklists (they only act together but there are two of them) are created when you launch Lethal Company and are destroyed when you close it. You can freely add/remove something from them and they will impact what you sell until you close the game or empty them.
- Active blacklist is the combination of the two. It takes the permanent blacklist, adds to it temporary(add) and removes temporary(remove) from it. It's the one which is ACTUALLY used to decide which items not to sell.

#### Priority

The priority list tells the mod which items to prioritize when selling. It will still try to sell as close to the provided value as possible but if multiple combinations of items are possible choose the one which has the most priority items. As with a blacklist there are three (four) kinds of priority sets: permanent, temporary(add), temporary(remove) and active.
- Permanent priority set loads itself from the config at the start of the game. Although it's possible, I wouldn't recommend to modify the config by yourself, instead modify the permanent priority set through in-game commands explained later (to avoid any user-made errors).
- Temporary priority sets (they only act together but there are two of them) are created when you launch Lethal Company and are destroyed when you close it. You can freely add/remove something from them and they will impact what you sell until you close the game or empty them.
- Active priority set is the combination of the two (three). It takes the permanent priority set, adds to it temporary(add) and removes temporary(remove) from it. It's the one which is ACTUALLY used to decide which items not to sell.

</details>


### Commands

<details>
  <summary>Click to collapse/expand</summary>

#### Item

Usage:

`/sell item [item]`

Sells all items with the specified name. If no name was specified then checks what item you are holding and gets it's name instead (and sells this held item too)

#### Quota

Usage:

`/sell quota [-a]`

Checks how much quota is left and tries to sell exactly that (if it's not enough, nothing will be sold and if exact value isn't achievable sells the smallest value after that)

#### All

Usage:

`/sell all [-a]`

Sells all (non-blacklisted, use -a to ignore blacklist) items

#### Amount

Usage:

`/sell <amount> [-o] [-e] [-a] [-n]`

Tries to sell exactly how much you specified. If there is not enough scrap, sells nothing. If an exact value isn't achievable sells the smallest value after that

#### Blacklist

Usage:

`/sell bl [-a] [-p]`

`/sell bl {add | ad | a | +} [itemName] [-p]`

`/sell bl {remove | rm | r | -} [itemName] [-p]`

`/sell bl {empty | flash | flush}`
                
Without modifiers just prints an active blacklist, you can add -a to also display temporary blacklist or -p to display permanent blacklist instead.
By using `/sell bl +` (`/sell bl -`) you can temporarily blacklist (or prohibit to blacklist) an item currently in your hands. You can also add/remove it from a permanent blacklist by using -p flag.
By using `/sell bl empty` you can clear temporary blacklist in case you don't need it anymore (keep in mind that it automatically resets when you close the game window)

#### Priority

Usage:

`/sell pr [-a] [-p]`
`/sell pr {add | ad | a | +} [itemName] [-p]`
`/sell pr {remove | rm | r | -} [itemName] [-p]`
`/sell pr {empty | flash | flush}`
                
Without modifiers just prints an active priority set, you can add -a to also display temporary priority set or -p to display permanent priority set instead.
By using "/sell pr +" ("/sell bl -") you can temporarily prioritize (or prohibit form being prioritized) an item currently in your hands. You can also add/remove it from a permanent priority set by using -p flag.
By using "/sell pr empty" you can clear temporary priority set in case you don't need it anymore (keep in mind that it automatically resets when you close the game window)

#### Overtime

Usage:

`/ot [-n]`

Displays overtime caused by already fullfilled quota and items on desk

</details>

### Flags

<details>
  <summary>Click to collapse/expand</summary>

### -o

Usage:

`/sell <amount> -o`

Respects the fact that your sold items can cause overtime and includes it in the calculations (note that the overtime caused by already sold items isn't included, you need -e flag for that) so that: requested value = final value in terminal (after leaving the planet) - existing money (look into -e help page for that)

### -e

Usage:

`/sell <amount> -e`

(Previously -t, but was changed to -e)
Removes existing money (already existing credits in terminal, items on desk and, if -o flag is present, future overtime based on these two) from your requsted value so that: requested value = final value in terminal (after leaving the planet) = existing money + sold items (+ overtime caused by sold items if -o flag is present)

### -a

Usage:

`/sell {quota | all | amount | bl | pr} -a`

When trying to find right items to sell, ignores all blacklists so that *EVERY* item can be sold. If used with "/sell bl" or "/sell pr" displays both temporary blacklists (or priority sets) along with the active one

### -p

Usage:

`/sell {bl | pr} [+ | -] -p`

When using the blacklist (or priority) command can be used to affect permanent blacklist (or priority set) instead of the temporary one

### -n

Usage:
`/sell <amount> -n`

Forces EVERY overtime calculation that occures during the execution of THIS command to think that there was no rehost after the final day of this quota, even if there was one). It is only needed if a host has a mod for late joining (aka LateCompany) and you joined after the final day of this quota (your client will think that there was a rehost then). There is no way (that I know of, at least, if you know one please tell me) to check if there was or wasn't a real rehost in this case, and if there wasn't, then all overtime calculations will be 15 smaller. This flag accounts for that, but note that if the rehost has actually occured and you used this flag then all overtime calculation will be 15 bigger so you should ask your host if they have done a rehost or not to get it right

</details>


## Usage example:

You landed on comapany after playing your final day of the 2nd quota, the quota is 256, you have 4 leftover credits in terminal and plan to buy jetpack (900), weed killer (25*2) and go to Artifice (1500). You can just use `/sell 900+25*2+1500 -eo`. The mod will try to sell 2094, the overtime because of that will be 352 and there already are 4 credits in terminal so overall it will be 2094+352+4=2450, exactly as requested. Then suppose you bought a jetpack and two weed killers and noted that you don't have any shovels so you decide to buy two of them (30*2). Now you can use `/sell 60 -o` to ignore how many credits and overtime you already have and get 60 more of credits+overtime. The mod will try to sell 50 and the overtime gained because of that will be 10, so overall it will be 60, exactly as requested.

## Credits

### Thanks to baer1 for YetAnotherSellMod which this mod originated from

### Also thanks to baer1 for ChatCommandAPI which was a very good tool to make chat commands with

### Thanks to Zehs for SellMyScrap which became a huge inspiration for me

### Thanks to NutNutty for logic in SellTracker which allows to track items on the desk on client