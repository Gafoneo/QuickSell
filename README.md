# QuickSell

## Usage:

There is specific information on each command and its usage in the in-game documentation (help pages)
- `/sell help [page]` to open general or specific help page
- `/sell item [item]` to sell all items like the one you are holding or the one you specified
- `/sell quota [-a]` to sell exactly for quota
- `/sell all [-a]` to sell all unfiltered scrap available
- `/sell <amount> [-o] [-t] [-a] [-n]` to sell exactly how much you need

## Flags:

- `-o` to sell accounting for overtime (used with <amount>)
- `-t` for accounting for existing money in terminal and overtime (used with <amount>)
- `-a` to ignore blacklist (used with quota, all, <amount>)
- `-n` to force non-restart overtime calculations (needed in rare edge cases)

## Reporting issues/feature requests
If you want to report a bug or request a new feature welcome to my [GitHub](https://github.com/Gafoneo/QuickSell/issues)