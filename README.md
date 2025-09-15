# CounterstrikeSharp - Grenade Info

[![UpdateManager Compatible](https://img.shields.io/badge/CS2-UpdateManager-darkgreen)](https://github.com/Kandru/cs2-update-manager/)
[![GitHub release](https://img.shields.io/github/release/Kandru/cs2-grenade-info?include_prereleases=&sort=semver&color=blue)](https://github.com/Kandru/cs2-grenade-info/releases/)
[![License](https://img.shields.io/badge/License-GPLv3-blue)](#license)
[![issues - cs2-map-modifier](https://img.shields.io/github/issues/Kandru/cs2-grenade-info)](https://github.com/Kandru/cs2-grenade-info/issues)
[![](https://www.paypalobjects.com/en_US/i/btn/btn_donateCC_LG.gif)](https://www.paypal.com/donate/?hosted_button_id=C2AVYKGVP9TRG)

GrenadeInfo is a fun Counter-Strike 2 plugin that tracks your grenade statistics and provides feedback on your utility usage. Whether you're curious about how many enemies you've blinded with flashbangs or want to see who throws the most grenades each round, this plugin adds an entertaining layer of information to your gameplay.

The plugin shows real-time notifications when you blind players, deal grenade damage, or accidentally flash teammates. At the end of each round, players can see their personal grenade statistics and check out a simple leaderboard highlighting the top performers. It's a lighthearted way to learn about your grenade habits and maybe discover some areas for improvement along the way.

## Key Features

- **Statistics Tracking**: See how many enemies you've blinded, grenades thrown, and damage dealt
- **Real-Time Notifications**: Get instant chat messages when you blind someone or deal grenade damage
- **Round Leaderboards**: Simple end-of-round summary showing the top 3 grenade users
- **Mistake Alerts**: Friendly reminders when you accidentally flash teammates or hurt yourself
- **Chat Commands**: Use `!gstats` to see your stats and `!topg` for the leaderboard
- **Easy Configuration**: Customize which features you want enabled through a simple config file

## Installation

1. Download and extract the latest release from the [GitHub releases page](https://github.com/Kandru/cs2-grenade-info/releases/).
2. Move the "GrenadeInfo" folder to the `/addons/counterstrikesharp/plugins/` directory.
3. Restart the server.

Updating is even easier: simply overwrite all plugin files and they will be reloaded automatically. To automate updates please use our [CS2 Update Manager](https://github.com/Kandru/cs2-update-manager/).


## Configuration

This plugin automatically creates a readable JSON configuration file. This configuration file can be found in `/addons/counterstrikesharp/configs/plugins/GrenadeInfo/GrenadeInfo.json`.

```json
{
  "enabled": true,
  "debug": false,
  "command_toplist": "topg",
  "command_personal_stats": "gstats",
  "info_message_limit": 6,
  "show_top_player_stats_on_round_end": true,
  "show_personal_stats_on_round_end": true,
  "show_flashevents_instantly": true,
  "show_grenade_damage_instantly": true,
  "ConfigVersion": 1
}
```

### enabled

Whether or not the plug-in is enabled.

### debug

Enables the debug mode. Only useful during development or if requested by the plug-in author.

### command_toplist

The command players can execute to show the toplist during gameplay. Defaults to `!topg` and can be emptied to disable this command.

### command_personal_stats

Shows the personal stats to the player. Defaults to `!gstats` and can be emptied to disable this command.

### info_message_limit

Amount of messages to display for the personal stats. Each message is one thing a player has achieved with grenades.

### show_top_player_stats_on_round_end

Whether or not to show the player stats on round end.

### show_personal_stats_on_round_end

Whether or not to show personal stats on round end.

### show_flashevents_instantly

Whether or not to show flashbang events in realtime for all involved players in chat.

### show_grenade_damage_instantly

Whether or not to show grenade damage in realtime for all involved players in chat.

## Commands

### (Server-Console only) grenadeinfo reload

Reload of the configuration file during runtime without reloading the plug-in.

### (Client chat only) !topg

Shows the top grenade players of the current round in chat.

### (Client chat only) !gstats

Shows the players personal grenade statistic of the current round in chat.

## Compile Yourself

Clone the project:

```bash
git clone https://github.com/Kandru/cs2-grenade-info.git
```

Go to the project directory

```bash
  cd cs2-grenade-info
```

Install dependencies

```bash
  dotnet restore
```

Build debug files (to use on a development game server)

```bash
  dotnet build
```

Build release files (to use on a production game server)

```bash
  dotnet publish
```

## FAQ

TODO

## License

Released under [GPLv3](/LICENSE) by [@Kandru](https://github.com/Kandru).

## Authors

- [@derkalle4](https://www.github.com/derkalle4)
- [@jmgraeffe](https://www.github.com/jmgraeffe)
