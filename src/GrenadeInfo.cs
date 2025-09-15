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
            float blindedByTotalAmount,
            int TotalGrenadesThrown,
            int TotalGrenadeDamageTaken,
            int TotalGrenadeDamageGiven,
            int totalGrenadeBounces)> _players = [];
        private (CCSPlayerController?, int) _lastFlashbang = (null, -1);
        private readonly List<string> _grenadeTypes = [
            "flashbang",
            "hegrenade",
            "smokegrenade",
            "molotov",
            "incgrenade",
            "inferno",
            "decoy",
            "tagrenade"
        ];

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
                _players[player] = (0, 0, 0, 0.0f, 0, 0, 0.0f, 0, 0, 0, 0);
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
            if (!Config.ShowPersonalStats)
            {
                return HookResult.Continue;
            }
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
                _players[player] = (0, 0, 0, 0.0f, 0, 0, 0.0f, 0, 0, 0, 0);
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
            CCSPlayerController? attacker = @event.Attacker;
            CCSPlayerController? victim = @event.Userid;
            if (attacker == null
                || !attacker.IsValid
                || victim == null
                || !victim.IsValid
                || !_grenadeTypes.Contains(@event.Weapon.ToLower())
                || !_players.TryGetValue(attacker, out (int blindedEnemies, int blindedTeam, int blindedSelf, float blindedTotalAmount, int blindedByEnemies, int blindedByTeam, float blindedByTotalAmount, int totalGrenadeDamageTaken, int totalGrenadeDamageGiven, int totalGrenadeBounces, int totalGrenadesThrown) stats))
            {
                return HookResult.Continue;
            }

            stats.totalGrenadeDamageGiven += @event.DmgHealth;
            _players[attacker] = stats;

            if (Config.ShowGrenadeDamageInstantly)
            {
                attacker.PrintToChat(Localizer["grenade.damage.given"].Value
                    .Replace("{damage}", @event.DmgHealth.ToString())
                    .Replace("{player}", victim.PlayerName));
                victim.PrintToChat(Localizer["grenade.damage.received"].Value
                    .Replace("{damage}", @event.DmgHealth.ToString())
                    .Replace("{player}", attacker.PlayerName));
            }
            return HookResult.Continue;
        }

        private HookResult OnGrenadeThrown(EventGrenadeThrown @event, GameEventInfo info)
        {
            CCSPlayerController? player = @event.Userid;
            if (player == null
                || !player.IsValid
                || !_players.TryGetValue(player, out (int blindedEnemies, int blindedTeam, int blindedSelf, float blindedTotalAmount, int blindedByEnemies, int blindedByTeam, float blindedByTotalAmount, int totalGrenadeDamageTaken, int totalGrenadeDamageGiven, int totalGrenadeBounces, int totalGrenadesThrown) stats))
            {
                return HookResult.Continue;
            }
            stats.totalGrenadesThrown += 1;
            _players[player] = stats;
            return HookResult.Continue;
        }

        private HookResult OnGrenadeBounced(EventGrenadeBounce @event, GameEventInfo info)
        {
            CCSPlayerController? player = @event.Userid;
            if (player == null
                || !player.IsValid
                || !_players.TryGetValue(player, out (int blindedEnemies, int blindedTeam, int blindedSelf, float blindedTotalAmount, int blindedByEnemies, int blindedByTeam, float blindedByTotalAmount, int totalGrenadeDamageTaken, int totalGrenadeDamageGiven, int totalGrenadeBounces, int totalGrenadesThrown) stats))
            {
                return HookResult.Continue;
            }
            stats.totalGrenadeBounces += 1;
            _players[player] = stats;
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
                || !_players.TryGetValue(player, out (int blindedEnemies, int blindedTeam, int blindedSelf, float blindedTotalAmount, int blindedByEnemies, int blindedByTeam, float blindedByTotalAmount, int totalGrenadeDamageTaken, int totalGrenadeDamageGiven, int totalGrenadeBounces, int totalGrenadesThrown) statsFlashed)
                || !_players.TryGetValue(_lastFlashbang.Item1!, out (int blindedEnemies, int blindedTeam, int blindedSelf, float blindedTotalAmount, int blindedByEnemies, int blindedByTeam, float blindedByTotalAmount, int totalGrenadeDamageTaken, int totalGrenadeDamageGiven, int totalGrenadeBounces, int totalGrenadesThrown) statsFlasher))
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
            else if (player.Team == _lastFlashbang.Item1.Team)
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
                if (Config.ShowFlashEventsInstantly)
                {
                    // show message to the flasher
                    _lastFlashbang.Item1.PrintToChat(Localizer["flashbang.given"].Value
                        .Replace("{player}", player.PlayerName)
                        .Replace("{sec}", @event.BlindDuration.ToString("0.0")));
                    // show message to the flashed player
                    player.PrintToChat(Localizer["flashbang.received"].Value
                        .Replace("{player}", _lastFlashbang.Item1.PlayerName)
                        .Replace("{sec}", @event.BlindDuration.ToString("0.0")));
                }
            }
            // save stats for flasher
            _players[_lastFlashbang.Item1!] = statsFlasher;
            // save stats for flashed player
            _players[player] = statsFlashed;

            return HookResult.Continue;
        }

        private void PrintGrenadeStats(CCSPlayerController player)
        {
            if (!_players.TryGetValue(player, out (int blindedEnemies, int blindedTeam, int blindedSelf, float blindedTotalAmount, int blindedByEnemies, int blindedByTeam, float blindedByTotalAmount, int totalGrenadeDamageTaken, int totalGrenadeDamageGiven, int totalGrenadeBounces, int totalGrenadesThrown) stats))
            {
                return;
            }

            // Check if player has any grenade activity at all
            if (stats.blindedEnemies == 0 && stats.blindedTeam == 0 && stats.blindedSelf == 0 &&
                stats.blindedByEnemies == 0 && stats.blindedByTeam == 0 &&
                stats.totalGrenadeDamageTaken == 0 && stats.totalGrenadeDamageGiven == 0
                && stats.totalGrenadeBounces == 0)
            {
                return;
            }

            // Create a priority-based list of statistics with their importance scores
            var statItems = new List<(string message, int priority, bool isNegative)>();

            // Critical negative stats (highest priority)
            if (stats.blindedSelf > 0)
            {
                statItems.Add((Localizer["flashbang.stats.self"].Value
                    .Replace("{count}", stats.blindedSelf.ToString()), 100, true));
            }

            if (stats.blindedTeam > 0)
            {
                statItems.Add((Localizer["flashbang.stats.team"].Value
                    .Replace("{count}", stats.blindedTeam.ToString()), 95, true));
            }

            // High-value positive stats
            if (stats.blindedEnemies > 0)
            {
                int priority = 90 + Math.Min(stats.blindedEnemies * 2, 10); // Bonus for multiple enemy flashes
                statItems.Add((Localizer["flashbang.stats.enemies"].Value
                    .Replace("{count}", stats.blindedEnemies.ToString()), priority, false));
            }

            if (stats.totalGrenadeDamageTaken > 0)
            {
                int priority = 85 + Math.Min(stats.totalGrenadeDamageTaken / 10, 15); // Bonus for high damage
                statItems.Add((Localizer["grenade.stats.damage.received"].Value
                    .Replace("{damage}", stats.totalGrenadeDamageTaken.ToString()), priority, false));
            }

            if (stats.totalGrenadeDamageGiven > 0)
            {
                int priority = 80 + Math.Min(stats.totalGrenadeDamageGiven / 10, 15); // Bonus for high damage
                statItems.Add((Localizer["grenade.stats.damage.given"].Value
                    .Replace("{damage}", stats.totalGrenadeDamageGiven.ToString()), priority, false));
            }

            // Medium priority stats
            if (stats.blindedTotalAmount > 0f)
            {
                int priority = 70 + Math.Min((int)(stats.blindedTotalAmount * 2), 20);
                statItems.Add((Localizer["flashbang.stats.total_blind_time_enemies"].Value
                    .Replace("{time}", stats.blindedTotalAmount.ToString("0.0")), priority, false));
            }

            if (stats.blindedByTeam > 0)
            {
                statItems.Add((Localizer["flashbang.stats.blinded_by_team"].Value
                    .Replace("{count}", stats.blindedByTeam.ToString()), 60, true));
            }

            if (stats.blindedByEnemies > 0)
            {
                statItems.Add((Localizer["flashbang.stats.blinded_by_enemies"].Value
                    .Replace("{count}", stats.blindedByEnemies.ToString()), 55, false));
            }

            // Lower priority stats
            if (stats.blindedByTotalAmount > 0f)
            {
                statItems.Add((Localizer["flashbang.stats.total_blind_time_self"].Value
                    .Replace("{time}", stats.blindedByTotalAmount.ToString("0.0")), 50, true));
            }

            if (stats.totalGrenadesThrown > 0)
            {
                int priority = 40 + Math.Min(stats.totalGrenadesThrown, 10);
                statItems.Add((Localizer["grenade.stats.thrown"].Value
                    .Replace("{count}", stats.totalGrenadesThrown.ToString()), priority, false));
            }

            if (stats.totalGrenadeBounces > 0)
            {
                int priority = 30 + Math.Min(stats.totalGrenadeBounces, 10);
                statItems.Add((Localizer["grenade.stats.bounces"].Value
                    .Replace("{count}", stats.totalGrenadeBounces.ToString()), priority, false));
            }

            // Sort by priority (descending) and prefer negative stats at same priority level
            var sortedStats = statItems
                .OrderByDescending(x => x.priority)
                .ThenByDescending(x => x.isNegative) // Show warnings first at same priority
                .Take(Config.InfoMessageLimit)
                .Select(x => x.message)
                .ToList();

            // Send combined message to player
            if (sortedStats.Count > 0)
            {
                string combinedMessage = Localizer["chat.prefix"].Value + string.Join(", ", sortedStats);
                player.PrintToChat(combinedMessage);
            }
        }
    }
}
