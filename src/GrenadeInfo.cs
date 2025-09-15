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
            int TotalGrenadeDamageTakenFromEnemies,
            int TotalGrenadeDamageTakenFromTeam,
            int TotalGrenadeDamageGivenToEnemies,
            int TotalGrenadeDamageGivenToTeam,
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
                _players[player] = (0, 0, 0, 0.0f, 0, 0, 0.0f, 0, 0, 0, 0, 0, 0);
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
                _players[player] = (0, 0, 0, 0.0f, 0, 0, 0.0f, 0, 0, 0, 0, 0, 0);
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
                || !_grenadeTypes.Contains(@event.Weapon.ToLower(System.Globalization.CultureInfo.CurrentCulture))
                || !_players.TryGetValue(attacker, out (int blindedEnemies, int blindedTeam, int blindedSelf, float blindedTotalAmount, int blindedByEnemies, int blindedByTeam, float blindedByTotalAmount, int totalGrenadesThrown, int totalGrenadeDamageTakenFromEnemies, int totalGrenadeDamageTakenFromTeam, int totalGrenadeDamageGivenToEnemies, int totalGrenadeDamageGivenToTeam, int totalGrenadeBounces) attackerStats))
            {
                return HookResult.Continue;
            }

            // Determine if it's team damage or enemy damage
            bool isTeamDamage = attacker.Team == victim.Team;

            // Update attacker's damage given
            if (isTeamDamage)
            {
                attackerStats.totalGrenadeDamageGivenToTeam += @event.DmgHealth;
            }
            else
            {
                attackerStats.totalGrenadeDamageGivenToEnemies += @event.DmgHealth;
            }
            _players[attacker] = attackerStats;

            // Update victim's damage taken
            if (_players.TryGetValue(victim, out (int blindedEnemies, int blindedTeam, int blindedSelf, float blindedTotalAmount, int blindedByEnemies, int blindedByTeam, float blindedByTotalAmount, int totalGrenadesThrown, int totalGrenadeDamageTakenFromEnemies, int totalGrenadeDamageTakenFromTeam, int totalGrenadeDamageGivenToEnemies, int totalGrenadeDamageGivenToTeam, int totalGrenadeBounces) victimStats))
            {
                if (isTeamDamage)
                {
                    victimStats.totalGrenadeDamageTakenFromTeam += @event.DmgHealth;
                }
                else
                {
                    victimStats.totalGrenadeDamageTakenFromEnemies += @event.DmgHealth;
                }
                _players[victim] = victimStats;
            }

            if (Config.ShowGrenadeDamageInstantly)
            {
                if (victim == attacker)
                {
                    attacker.PrintToChat(Localizer["grenade.damage.self"].Value
                        .Replace("{damage}", @event.DmgHealth.ToString()));
                    return HookResult.Continue;
                }
                else if (attacker.Team == victim.Team)
                {
                    attacker.PrintToChat(Localizer["grenade.damage.given.team"].Value
                        .Replace("{damage}", @event.DmgHealth.ToString())
                        .Replace("{player}", victim.PlayerName));
                    victim.PrintToChat(Localizer["grenade.damage.received.team"].Value
                        .Replace("{damage}", @event.DmgHealth.ToString())
                        .Replace("{player}", attacker.PlayerName));
                }
                else
                {
                    attacker.PrintToChat(Localizer["grenade.damage.given.enemies"].Value
                        .Replace("{damage}", @event.DmgHealth.ToString())
                        .Replace("{player}", victim.PlayerName));
                    victim.PrintToChat(Localizer["grenade.damage.received.enemies"].Value
                        .Replace("{damage}", @event.DmgHealth.ToString())
                        .Replace("{player}", attacker.PlayerName));
                }
            }
            return HookResult.Continue;
        }

        private HookResult OnGrenadeThrown(EventGrenadeThrown @event, GameEventInfo info)
        {
            CCSPlayerController? player = @event.Userid;
            if (player == null
                || !player.IsValid
                || !_players.TryGetValue(player, out (int blindedEnemies, int blindedTeam, int blindedSelf, float blindedTotalAmount, int blindedByEnemies, int blindedByTeam, float blindedByTotalAmount, int totalGrenadesThrown, int totalGrenadeDamageTakenFromEnemies, int totalGrenadeDamageTakenFromTeam, int totalGrenadeDamageGivenToEnemies, int totalGrenadeDamageGivenToTeam, int totalGrenadeBounces) stats))
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
                || !_players.TryGetValue(player, out (int blindedEnemies, int blindedTeam, int blindedSelf, float blindedTotalAmount, int blindedByEnemies, int blindedByTeam, float blindedByTotalAmount, int totalGrenadesThrown, int totalGrenadeDamageTakenFromEnemies, int totalGrenadeDamageTakenFromTeam, int totalGrenadeDamageGivenToEnemies, int totalGrenadeDamageGivenToTeam, int totalGrenadeBounces) stats))
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
                || !_lastFlashbang.Item1.IsValid || player == null
                || !player.IsValid
                || !_players.TryGetValue(player, out (int blindedEnemies, int blindedTeam, int blindedSelf, float blindedTotalAmount, int blindedByEnemies, int blindedByTeam, float blindedByTotalAmount, int totalGrenadesThrown, int totalGrenadeDamageTakenFromEnemies, int totalGrenadeDamageTakenFromTeam, int totalGrenadeDamageGivenToEnemies, int totalGrenadeDamageGivenToTeam, int totalGrenadeBounces) statsFlashed)
                || !_players.TryGetValue(_lastFlashbang.Item1!, out (int blindedEnemies, int blindedTeam, int blindedSelf, float blindedTotalAmount, int blindedByEnemies, int blindedByTeam, float blindedByTotalAmount, int totalGrenadesThrown, int totalGrenadeDamageTakenFromEnemies, int totalGrenadeDamageTakenFromTeam, int totalGrenadeDamageGivenToEnemies, int totalGrenadeDamageGivenToTeam, int totalGrenadeBounces) statsFlasher))
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
            if (!_players.TryGetValue(player, out (int blindedEnemies, int blindedTeam, int blindedSelf, float blindedTotalAmount, int blindedByEnemies, int blindedByTeam, float blindedByTotalAmount, int totalGrenadesThrown, int totalGrenadeDamageTakenFromEnemies, int totalGrenadeDamageTakenFromTeam, int totalGrenadeDamageGivenToEnemies, int totalGrenadeDamageGivenToTeam, int totalGrenadeBounces) stats))
            {
                return;
            }

            // Check if player has any grenade activity at all
            if (stats.blindedEnemies == 0 && stats.blindedTeam == 0 && stats.blindedSelf == 0 &&
                stats.blindedByEnemies == 0 && stats.blindedByTeam == 0 &&
                stats.totalGrenadeDamageTakenFromEnemies == 0 && stats.totalGrenadeDamageTakenFromTeam == 0 &&
                stats.totalGrenadeDamageGivenToEnemies == 0 && stats.totalGrenadeDamageGivenToTeam == 0
                && stats.totalGrenadeBounces == 0)
            {
                return;
            }

            // Define stat categories with proper priorities and types
            List<(string message, int basePriority, int value)> negativeStats = [];
            List<(string message, int basePriority, int value)> positiveStats = [];

            // === NEGATIVE STATS (Learning opportunities - RED) ===
            // Critical mistakes (highest priority)
            if (stats.blindedSelf > 0)
            {
                negativeStats.Add((Localizer["flashbang.stats.self"].Value
                    .Replace("{count}", stats.blindedSelf.ToString()), 100, stats.blindedSelf));
            }

            if (stats.blindedTeam > 0)
            {
                negativeStats.Add((Localizer["flashbang.stats.team"].Value
                    .Replace("{count}", stats.blindedTeam.ToString()), 95, stats.blindedTeam));
            }

            // Team damage given (very high priority - serious mistake)
            if (stats.totalGrenadeDamageGivenToTeam > 0)
            {
                negativeStats.Add((Localizer["grenade.stats.damage.given.team"].Value
                    .Replace("{damage}", stats.totalGrenadeDamageGivenToTeam.ToString()), 92, stats.totalGrenadeDamageGivenToTeam));
            }

            // Damage taken from enemies (high priority - learn to avoid grenades)
            if (stats.totalGrenadeDamageTakenFromEnemies > 0)
            {
                negativeStats.Add((Localizer["grenade.stats.damage.received.enemies"].Value
                    .Replace("{damage}", stats.totalGrenadeDamageTakenFromEnemies.ToString()), 90, stats.totalGrenadeDamageTakenFromEnemies));
            }

            // Damage taken from team (high priority - team coordination issue)
            if (stats.totalGrenadeDamageTakenFromTeam > 0)
            {
                negativeStats.Add((Localizer["grenade.stats.damage.received.team"].Value
                    .Replace("{damage}", stats.totalGrenadeDamageTakenFromTeam.ToString()), 88, stats.totalGrenadeDamageTakenFromTeam));
            }

            // Being flashed by others (medium priority)
            if (stats.blindedByTeam > 0)
            {
                negativeStats.Add((Localizer["flashbang.stats.blinded_by_team"].Value
                    .Replace("{count}", stats.blindedByTeam.ToString()), 80, stats.blindedByTeam));
            }

            if (stats.blindedByEnemies > 0)
            {
                negativeStats.Add((Localizer["flashbang.stats.blinded_by_enemies"].Value
                    .Replace("{count}", stats.blindedByEnemies.ToString()), 70, stats.blindedByEnemies));
            }

            // Time spent blinded (lower priority)
            if (stats.blindedByTotalAmount > 0f)
            {
                negativeStats.Add((Localizer["flashbang.stats.total_blind_time_self"].Value
                    .Replace("{time}", stats.blindedByTotalAmount.ToString("0.1")), 60, (int)stats.blindedByTotalAmount));
            }

            // === POSITIVE STATS (Achievements - GREEN) ===
            // High impact positive actions
            if (stats.blindedEnemies > 0)
            {
                positiveStats.Add((Localizer["flashbang.stats.enemies"].Value
                    .Replace("{count}", stats.blindedEnemies.ToString()), 90, stats.blindedEnemies));
            }

            if (stats.totalGrenadeDamageGivenToEnemies > 0)
            {
                positiveStats.Add((Localizer["grenade.stats.damage.given.enemies"].Value
                    .Replace("{damage}", stats.totalGrenadeDamageGivenToEnemies.ToString()), 85, stats.totalGrenadeDamageGivenToEnemies));
            }

            // Efficiency stats
            if (stats.blindedTotalAmount > 0f)
            {
                positiveStats.Add((Localizer["flashbang.stats.total_blind_time_enemies"].Value
                    .Replace("{time}", stats.blindedTotalAmount.ToString("0.1")), 80, (int)stats.blindedTotalAmount));
            }

            // Activity stats (lower priority)
            if (stats.totalGrenadesThrown > 0)
            {
                positiveStats.Add((Localizer["grenade.stats.thrown"].Value
                    .Replace("{count}", stats.totalGrenadesThrown.ToString()), 40, stats.totalGrenadesThrown));
            }

            if (stats.totalGrenadeBounces > 0)
            {
                positiveStats.Add((Localizer["grenade.stats.bounces"].Value
                    .Replace("{count}", stats.totalGrenadeBounces.ToString()), 30, stats.totalGrenadeBounces));
            }

            // Calculate dynamic priorities with bonuses
            List<(string message, int priority, bool isNegative)> finalStats = [];

            // Add negative stats with dynamic priority bonuses
            foreach ((string? message, int basePriority, int value) in negativeStats)
            {
                int dynamicPriority = basePriority + Math.Min(value * 2, 20); // Bonus for severity
                finalStats.Add((message, dynamicPriority, true));
            }

            // Add positive stats with dynamic priority bonuses
            foreach ((string? message, int basePriority, int value) in positiveStats)
            {
                int dynamicPriority = basePriority + Math.Min(value, 15); // Smaller bonus for achievements
                finalStats.Add((message, dynamicPriority, false));
            }

            // Sort: Negative stats first (within same priority), then by priority descending
            List<string> sortedStats = [.. finalStats
                .OrderByDescending(static x => x.isNegative ? x.priority + 1000 : x.priority) // Negative stats get priority boost
                .Take(Config.InfoMessageLimit)
                .Select(static x => x.message)];

            // Send combined message to player
            if (sortedStats.Count > 0)
            {
                string combinedMessage = Localizer["chat.prefix"].Value + string.Join(", ", sortedStats);
                player.PrintToChat(combinedMessage);
            }
        }
    }
}
