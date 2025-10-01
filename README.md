# QuickSell


## TL;DR:

If you need an <amount> of money overall use `/sell <amount> -to`. If you need to have <amount> more than you already have: use `/sell <amount> -o`. If you just wanna sell some items worth <amount> use `/sell <amount>`. If you wanna sell exactly to quota use `/sell quota`. If you wanna sell all items on ship use `/sell all`. If you wanna sell one TYPE of item, hold this item in your hands and use `/sell item`


## Usage:


There is specific information on each command and its usage in the in-game documentation (help pages)

- `/sell help [page]` to open general or specific help page

- `/sell help pages` to open a list of all help pages

- `/sell item [item]` to sell all items like the one you are holding or the one you specified

- `/sell quota [-a]` to sell exactly for quota

- `/sell all [-a]` to sell all unfiltered scrap available

- `/sell <amount> [-o] [-t] [-a] [-n]` to sell exactly how much you need


## Flags:


Again there are help pages dedicated to each flag and to flags in general

- `-o` to sell accounting for overtime (used with <amount> and often goes together with -t)

- `-t` for accounting for existing money in terminal and overtime (used with <amount> and almost always goes together with -o)

- `-a` to ignore blacklist (used with quota, all, <amount>)

- `-n` to force non-restart overtime calculations (needed in very rare edge cases with late joining mods after late joining as a client)


## Usage example:

You landed on comapany after playing your final day of the 2nd quota, the quota is 256, you have 4 leftover credits in terminal and plan to buy jetpack (900), weed killer (25*2) and go to Artifice (1500). You can just use `/sell 900+25*2+1500 -to`. The mod will try to sell 2094, the overtime because of that will be 352 and there already are 4 credits in terminal so overall it will be 2094+352+4=2450, exactly as requested. Then suppose you bought a jetpack and two weed killers and noted that you don't have any shovels so you decide to buy two of them (30*2). Now you can use `/sell 60 -o` to ignore how many credits and overtime you already have and get 60 more of credits+overtime. The mod will try to sell 50 and the overtime gained because of that will be 10, so overall it will be 60, exactly as requested.
