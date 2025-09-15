using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Extensions;

namespace GrenadeInfo
{
    public partial class GrenadeInfo
    {
        [ConsoleCommand("grenadeinfo", "GrenadeInfo admin commands")]
        [CommandHelper(whoCanExecute: CommandUsage.SERVER_ONLY, minArgs: 1, usage: "<command>")]
        public void CommandMapVote(CCSPlayerController player, CommandInfo command)
        {
            string subCommand = command.GetArg(1);
            switch (subCommand.ToLower())
            {
                case "reload":
                    Config.Reload();
                    command.ReplyToCommand(Localizer["admin.reload"]);
                    break;
                default:
                    command.ReplyToCommand(Localizer["admin.unknown_command"].Value
                        .Replace("{command}", subCommand));
                    break;
            }
        }

        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY, minArgs: 0, usage: "")]
        public void CommandGrenadePersonalStats(CCSPlayerController? player, CommandInfo command)
        {
            if (player == null
                || !player.IsValid)
            {
                return;
            }
            PrintGrenadeStats(player);
        }

        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY, minArgs: 0, usage: "")]
        public void CommandGrenadeTopStats(CCSPlayerController? player, CommandInfo command)
        {
            PrintTopPlayersStats();
        }
    }
}
