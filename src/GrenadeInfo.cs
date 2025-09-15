using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;

namespace GrenadeInfo
{
    public partial class GrenadeInfo : BasePlugin
    {
        public override string ModuleName => "CS2 GrenadeInfo";
        public override string ModuleAuthor => "Kalle <kalle@kandru.de>";

        private readonly Dictionary<CCSPlayerController,
            (int blindedEnemies,
            int blindedTeam,
            int blindedSelf,
            float blindedTotalAmount,
            int blindedByEnemies,
            int blindedByTeam,
            float blindedByTotalAmount)> _players = [];
        private (CCSPlayerController?, int) _lastFlashbang = (null, -1);

        public override void Load(bool hotReload)
        {
            // register all event handlers
            RegisterEventHandler<EventRoundStart>(OnRoundStart);
            RegisterEventHandler<EventRoundEnd>(OnRoundEnd);
            RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);
            RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);
            RegisterEventHandler<EventPlayerHurt>(OnPlayerHurt);
            RegisterEventHandler<EventGrenadeThrown>(OnGrenadeThrown);
            RegisterEventHandler<EventGrenadeBounce>(OnGrenadeBounced);
            RegisterEventHandler<EventFlashbangDetonate>(OnFlashbangDetonated);
            RegisterEventHandler<EventPlayerBlind>(OnPlayerBlinded);
            // get all players currently on the server
            foreach (CCSPlayerController player in Utilities.GetPlayers())
            {
                _players[player] = (0, 0, 0, 0.0f, 0, 0, 0.0f);
            }
        }

        public override void Unload(bool hotReload)
        {
            // unregister all event handlers
            DeregisterEventHandler<EventRoundStart>(OnRoundStart);
            DeregisterEventHandler<EventRoundEnd>(OnRoundEnd);
            DeregisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);
            DeregisterEventHandler<EventPlayerHurt>(OnPlayerHurt);
            DeregisterEventHandler<EventGrenadeThrown>(OnGrenadeThrown);
            DeregisterEventHandler<EventGrenadeBounce>(OnGrenadeBounced);
            DeregisterEventHandler<EventFlashbangDetonate>(OnFlashbangDetonated);
            DeregisterEventHandler<EventPlayerBlind>(OnPlayerBlinded);
            // reset state
            Reset();
        }

        private void Reset()
        {
            _lastFlashbang = (null, -1);
            _players.Clear();
        }

        private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
        {
            // reset state on round start
            Reset();
            return HookResult.Continue;
        }

        private HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
        {
            // print statistics for all players
            foreach (CCSPlayerController player in Utilities.GetPlayers().Where(static p => !p.IsBot && !p.IsHLTV))
            {
                PrintGrenadeStats(player);
            }
            return HookResult.Continue;
        }

        private HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
        {
            CCSPlayerController? player = @event.Userid;
            if (player == null
                || !player.IsValid)
            {
                return HookResult.Continue;
            }

            if (!_players.ContainsKey(player))
            {
                _players[player] = (0, 0, 0, 0.0f, 0, 0, 0.0f);
            }
            return HookResult.Continue;
        }

        private HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
        {
            CCSPlayerController? player = @event.Userid;
            if (player == null
                || !player.IsValid)
            {
                return HookResult.Continue;
            }

            _ = _players.Remove(player);
            return HookResult.Continue;
        }

        private HookResult OnPlayerHurt(EventPlayerHurt @event, GameEventInfo info)
        {
            return HookResult.Continue;
        }

        private HookResult OnGrenadeThrown(EventGrenadeThrown @event, GameEventInfo info)
        {
            return HookResult.Continue;
        }

        private HookResult OnGrenadeBounced(EventGrenadeBounce @event, GameEventInfo info)
        {
            return HookResult.Continue;
        }

        private HookResult OnFlashbangDetonated(EventFlashbangDetonate @event, GameEventInfo info)
        {
            _lastFlashbang = (@event.Userid, @event.Entityid);
            return HookResult.Continue;
        }

        private HookResult OnPlayerBlinded(EventPlayerBlind @event, GameEventInfo info)
        {
            CCSPlayerController? player = @event.Userid;
            if (_lastFlashbang.Item2 == -1
                || _lastFlashbang.Item1 == null
                || _lastFlashbang.Item1.IsValid == false
                || player == null
                || !player.IsValid
                || !_players.TryGetValue(player, out (int blindedEnemies, int blindedTeam, int blindedSelf, float blindedTotalAmount, int blindedByEnemies, int blindedByTeam, float blindedByTotalAmount) statsFlashed)
                || !_players.TryGetValue(_lastFlashbang.Item1!, out (int blindedEnemies, int blindedTeam, int blindedSelf, float blindedTotalAmount, int blindedByEnemies, int blindedByTeam, float blindedByTotalAmount) statsFlasher))
            {
                return HookResult.Continue;
            }
            // check if flashed self
            if (player == _lastFlashbang.Item1)
            {
                // set stats for flashed player
                statsFlashed.blindedSelf += 1;
                statsFlashed.blindedByTotalAmount += @event.BlindDuration;
                statsFlasher.blindedSelf += 1;
                statsFlasher.blindedByTotalAmount += @event.BlindDuration;
                // show message to the flasher
                _lastFlashbang.Item1.PrintToChat(Localizer["flashbang.selfflash"].Value
                    .Replace("{sec}", @event.BlindDuration.ToString("0.0")));
            }
            // check if flashed team
            else if (player.Team == _lastFlashbang.Item1?.Team)
            {
                // set stats for flashed player
                statsFlashed.blindedByTeam += 1;
                statsFlashed.blindedByTotalAmount += @event.BlindDuration;
                // set stats for flasher
                statsFlasher.blindedTeam += 1;
                statsFlasher.blindedTotalAmount += @event.BlindDuration;
                // show message to the flasher
                _lastFlashbang.Item1.PrintToChat(Localizer["flashbang.teamflash.given"].Value
                    .Replace("{player}", player.PlayerName)
                    .Replace("{sec}", @event.BlindDuration.ToString("0.0")));
                // show message to the flashed player
                player.PrintToChat(Localizer["flashbang.teamflash.received"].Value
                    .Replace("{player}", _lastFlashbang.Item1.PlayerName)
                    .Replace("{sec}", @event.BlindDuration.ToString("0.0")));
            }
            // else flashed enemy
            else
            {
                // set stats for flashed player
                statsFlashed.blindedByEnemies += 1;
                statsFlashed.blindedByTotalAmount += @event.BlindDuration;
                // set stats for flasher
                statsFlasher.blindedEnemies += 1;
                statsFlasher.blindedTotalAmount += @event.BlindDuration;
            }
            // save stats for flasher
            _players[_lastFlashbang.Item1!] = statsFlasher;
            // save stats for flashed player
            _players[player] = statsFlashed;

            Console.WriteLine($"[GrenadeInfo] {player.PlayerName} was blinded by {statsFlasher.blindedByTotalAmount}, {statsFlashed.blindedByTotalAmount}");
            return HookResult.Continue;
        }

        private void PrintGrenadeStats(CCSPlayerController player)
        {
            if (!_players.TryGetValue(player, out (int blindedEnemies, int blindedTeam, int blindedSelf, float blindedTotalAmount, int blindedByEnemies, int blindedByTeam, float blindedByTotalAmount) stats))
            {
                return;
            }

            // Check if player has any flashbang activity
            if (stats.blindedEnemies == 0 && stats.blindedTeam == 0 && stats.blindedSelf == 0 &&
                stats.blindedByEnemies == 0 && stats.blindedByTeam == 0)
            {
                return;
            }

            List<string> messages = [];

            // 1. Self-flashing warning
            if (stats.blindedSelf > 0)
            {
                messages.Add(Localizer["flashbang.stats.self"].Value
                    .Replace("{count}", stats.blindedSelf.ToString()));
            }
            // 2. Team-flashing warning
            if (stats.blindedTeam > 0)
            {
                messages.Add(Localizer["flashbang.stats.team"].Value
                    .Replace("{count}", stats.blindedTeam.ToString()));
            }
            // 3. Enemy flashing (positive)
            if (stats.blindedEnemies > 0)
            {
                messages.Add(Localizer["flashbang.stats.enemies"].Value
                    .Replace("{count}", stats.blindedEnemies.ToString()));
            }
            // Limit to Config.InfoMessageLimit messages for flashbangs, but add "got flashed" stats if space available
            if (messages.Count < Config.InfoMessageLimit)
            {
                // 4. Got flashed by team
                if (stats.blindedByTeam > 0)
                {
                    messages.Add(Localizer["flashbang.stats.blinded_by_team"].Value
                        .Replace("{count}", stats.blindedByTeam.ToString()));
                }

                // 5. Got flashed by enemies (only if still space)
                if (messages.Count < Config.InfoMessageLimit && stats.blindedByEnemies > 0)
                {
                    messages.Add(Localizer["flashbang.stats.blinded_by_enemies"].Value
                        .Replace("{count}", stats.blindedByEnemies.ToString()));
                }
            }

            if (stats.blindedTotalAmount > 0f)
            {
                messages.Add(Localizer["flashbang.stats.total_blind_time_enemies"].Value
                    .Replace("{time}", stats.blindedTotalAmount.ToString("0.0")));
            }
            if (stats.blindedByTotalAmount > 0f)
            {
                messages.Add(Localizer["flashbang.stats.total_blind_time_self"].Value
                    .Replace("{time}", stats.blindedByTotalAmount.ToString("0.0")));
            }

            // Send combined message to player
            if (messages.Count > 0)
            {
                string combinedMessage = Localizer["chat.prefix"].Value + string.Join(", ", messages);
                player.PrintToChat(combinedMessage);
            }
        }
    }
}
