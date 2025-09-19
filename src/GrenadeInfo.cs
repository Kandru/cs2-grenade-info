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
            int TotalGrenadeDamageTakenSelf,
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
            RegisterEventHandler<EventWarmupEnd>(OnWarmupEnd);
            RegisterEventHandler<EventRoundEnd>(OnRoundEnd);
            RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);
            RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);
            RegisterEventHandler<EventPlayerHurt>(OnPlayerHurt);
            RegisterEventHandler<EventGrenadeThrown>(OnGrenadeThrown);
            RegisterEventHandler<EventGrenadeBounce>(OnGrenadeBounced);
            RegisterEventHandler<EventFlashbangDetonate>(OnFlashbangDetonated);
            RegisterEventHandler<EventPlayerBlind>(OnPlayerBlinded);
            RegisterListener<Listeners.OnMapEnd>(OnMapEnd);
            // add chat commands
            if (!string.IsNullOrWhiteSpace(Config.CommandPersonalStats))
            {
                AddCommand(Config.CommandPersonalStats,
                "Show personal grenade statistics",
                CommandGrenadePersonalStats);
            }
            if (!string.IsNullOrWhiteSpace(Config.CommandToplist))
            {
                AddCommand(Config.CommandToplist,
                "Show top list about grenades",
                CommandGrenadeTopStats);
            }
            if (hotReload)
            {
                // get all players currently on the server
                foreach (CCSPlayerController player in Utilities.GetPlayers())
                {
                    _players[player] = (0, 0, 0, 0.0f, 0, 0, 0.0f, 0, 0, 0, 0, 0, 0, 0);
                }
            }
        }

        public override void Unload(bool hotReload)
        {
            // unregister all event handlers
            DeregisterEventHandler<EventWarmupEnd>(OnWarmupEnd);
            DeregisterEventHandler<EventRoundEnd>(OnRoundEnd);
            DeregisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);
            DeregisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);
            DeregisterEventHandler<EventPlayerHurt>(OnPlayerHurt);
            DeregisterEventHandler<EventGrenadeThrown>(OnGrenadeThrown);
            DeregisterEventHandler<EventGrenadeBounce>(OnGrenadeBounced);
            DeregisterEventHandler<EventFlashbangDetonate>(OnFlashbangDetonated);
            DeregisterEventHandler<EventPlayerBlind>(OnPlayerBlinded);
            RemoveListener<Listeners.OnMapEnd>(OnMapEnd);

            // remove chat commands
            if (!string.IsNullOrWhiteSpace(Config.CommandPersonalStats))
            {
                RemoveCommand(Config.CommandPersonalStats, CommandGrenadePersonalStats);
            }
            if (!string.IsNullOrWhiteSpace(Config.CommandToplist))
            {
                RemoveCommand(Config.CommandToplist, CommandGrenadeTopStats);
            }
            // reset state
            Reset();
        }

        private void Reset()
        {
            _lastFlashbang = (null, -1);
            _players.Clear();
        }

        private HookResult OnWarmupEnd(EventWarmupEnd @event, GameEventInfo info)
        {
            // reset state on warmup end
            Reset();
            return HookResult.Continue;
        }

        private HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
        {
            if (!Config.Enabled)
            {
                return HookResult.Continue;
            }
            if (Config.ShowTopPlayerStatsOnRoundEnd)
            {
                // Show top players leaderboard first
                PrintTopPlayersStats();
            }
            if (Config.ShowPersonalStatsOnRoundEnd)
            {
                // Then print individual statistics for all players
                foreach (CCSPlayerController player in Utilities.GetPlayers().Where(static p => !p.IsBot && !p.IsHLTV))
                {
                    PrintGrenadeStats(player);
                }
            }
            // reset state on round start
            Reset();
            return HookResult.Continue;
        }

        private HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
        {
            if (!Config.Enabled)
            {
                return HookResult.Continue;
            }
            CCSPlayerController? player = @event.Userid;
            if (player == null
                || !player.IsValid)
            {
                return HookResult.Continue;
            }

            if (!_players.ContainsKey(player))
            {
                _players[player] = (0, 0, 0, 0.0f, 0, 0, 0.0f, 0, 0, 0, 0, 0, 0, 0);
                Console.WriteLine($"Player {player.PlayerName} spawned.");
            }
            return HookResult.Continue;
        }

        private HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
        {
            if (!Config.Enabled)
            {
                return HookResult.Continue;
            }
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
            if (!Config.Enabled)
            {
                return HookResult.Continue;
            }
            CCSPlayerController? attacker = @event.Attacker;
            CCSPlayerController? victim = @event.Userid;
            if (attacker == null
                || !attacker.IsValid
                || victim == null
                || !victim.IsValid
                || !_grenadeTypes.Contains(@event.Weapon.ToLower(System.Globalization.CultureInfo.CurrentCulture))
                || !_players.TryGetValue(attacker, out (int blindedEnemies, int blindedTeam, int blindedSelf, float blindedTotalAmount, int blindedByEnemies, int blindedByTeam, float blindedByTotalAmount, int totalGrenadesThrown, int totalGrenadeDamageTakenFromEnemies, int totalGrenadeDamageTakenFromTeam, int totalGrenadeDamageTakenSelf, int totalGrenadeDamageGivenToEnemies, int totalGrenadeDamageGivenToTeam, int totalGrenadeBounces) attackerStats))
            {
                return HookResult.Continue;
            }

            // Determine if it's team damage, enemy damage, or self damage
            bool isSelfDamage = attacker == victim;
            bool isTeamDamage = !isSelfDamage && attacker.Team == victim.Team;

            // Update attacker's damage given (no self-damage tracking here as it's handled in victim stats)
            if (!isSelfDamage)
            {
                if (isTeamDamage)
                {
                    attackerStats.totalGrenadeDamageGivenToTeam += @event.DmgHealth;
                }
                else
                {
                    attackerStats.totalGrenadeDamageGivenToEnemies += @event.DmgHealth;
                }
                _players[attacker] = attackerStats;
            }

            // Update victim's damage taken
            if (_players.TryGetValue(victim, out (int blindedEnemies, int blindedTeam, int blindedSelf, float blindedTotalAmount, int blindedByEnemies, int blindedByTeam, float blindedByTotalAmount, int totalGrenadesThrown, int totalGrenadeDamageTakenFromEnemies, int totalGrenadeDamageTakenFromTeam, int totalGrenadeDamageTakenSelf, int totalGrenadeDamageGivenToEnemies, int totalGrenadeDamageGivenToTeam, int totalGrenadeBounces) victimStats))
            {
                if (isSelfDamage)
                {
                    victimStats.totalGrenadeDamageTakenSelf += @event.DmgHealth;
                }
                else if (isTeamDamage)
                {
                    victimStats.totalGrenadeDamageTakenFromTeam += @event.DmgHealth;
                }
                else
                {
                    victimStats.totalGrenadeDamageTakenFromEnemies += @event.DmgHealth;
                }
                _players[victim] = victimStats;
            }

            if (Config.ShowGrenadeDamageInstantly && !@event.Weapon.Equals("inferno", StringComparison.OrdinalIgnoreCase))
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
            if (!Config.Enabled)
            {
                return HookResult.Continue;
            }
            CCSPlayerController? player = @event.Userid;
            if (player == null
                || !player.IsValid
                || !_players.TryGetValue(player, out (int blindedEnemies, int blindedTeam, int blindedSelf, float blindedTotalAmount, int blindedByEnemies, int blindedByTeam, float blindedByTotalAmount, int totalGrenadesThrown, int totalGrenadeDamageTakenFromEnemies, int totalGrenadeDamageTakenFromTeam, int totalGrenadeDamageTakenSelf, int totalGrenadeDamageGivenToEnemies, int totalGrenadeDamageGivenToTeam, int totalGrenadeBounces) stats))
            {
                return HookResult.Continue;
            }
            stats.totalGrenadesThrown += 1;
            _players[player] = stats;
            return HookResult.Continue;
        }

        private HookResult OnGrenadeBounced(EventGrenadeBounce @event, GameEventInfo info)
        {
            if (!Config.Enabled)
            {
                return HookResult.Continue;
            }
            CCSPlayerController? player = @event.Userid;
            if (player == null
                || !player.IsValid
                || !_players.TryGetValue(player, out (int blindedEnemies, int blindedTeam, int blindedSelf, float blindedTotalAmount, int blindedByEnemies, int blindedByTeam, float blindedByTotalAmount, int totalGrenadesThrown, int totalGrenadeDamageTakenFromEnemies, int totalGrenadeDamageTakenFromTeam, int totalGrenadeDamageTakenSelf, int totalGrenadeDamageGivenToEnemies, int totalGrenadeDamageGivenToTeam, int totalGrenadeBounces) stats))
            {
                return HookResult.Continue;
            }
            stats.totalGrenadeBounces += 1;
            _players[player] = stats;
            return HookResult.Continue;
        }

        private HookResult OnFlashbangDetonated(EventFlashbangDetonate @event, GameEventInfo info)
        {
            if (!Config.Enabled)
            {
                return HookResult.Continue;
            }
            _lastFlashbang = (@event.Userid, @event.Entityid);
            return HookResult.Continue;
        }

        private HookResult OnPlayerBlinded(EventPlayerBlind @event, GameEventInfo info)
        {
            if (!Config.Enabled)
            {
                return HookResult.Continue;
            }
            CCSPlayerController? player = @event.Userid;
            if (_lastFlashbang.Item2 == -1
                || _lastFlashbang.Item1 == null
                || !_lastFlashbang.Item1.IsValid || player == null
                || !player.IsValid
                || !_players.TryGetValue(player, out (int blindedEnemies, int blindedTeam, int blindedSelf, float blindedTotalAmount, int blindedByEnemies, int blindedByTeam, float blindedByTotalAmount, int totalGrenadesThrown, int totalGrenadeDamageTakenFromEnemies, int totalGrenadeDamageTakenFromTeam, int totalGrenadeDamageTakenSelf, int totalGrenadeDamageGivenToEnemies, int totalGrenadeDamageGivenToTeam, int totalGrenadeBounces) statsFlashed)
                || !_players.TryGetValue(_lastFlashbang.Item1!, out (int blindedEnemies, int blindedTeam, int blindedSelf, float blindedTotalAmount, int blindedByEnemies, int blindedByTeam, float blindedByTotalAmount, int totalGrenadesThrown, int totalGrenadeDamageTakenFromEnemies, int totalGrenadeDamageTakenFromTeam, int totalGrenadeDamageTakenSelf, int totalGrenadeDamageGivenToEnemies, int totalGrenadeDamageGivenToTeam, int totalGrenadeBounces) statsFlasher))
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

        private void OnMapEnd()
        {
            // reset state on map end
            Reset();
        }

        private void PrintGrenadeStats(CCSPlayerController player)
        {
            if (!_players.TryGetValue(player, out (int blindedEnemies, int blindedTeam, int blindedSelf, float blindedTotalAmount, int blindedByEnemies, int blindedByTeam, float blindedByTotalAmount, int totalGrenadesThrown, int totalGrenadeDamageTakenFromEnemies, int totalGrenadeDamageTakenFromTeam, int totalGrenadeDamageTakenSelf, int totalGrenadeDamageGivenToEnemies, int totalGrenadeDamageGivenToTeam, int totalGrenadeBounces) stats))
            {
                return;
            }

            // Check if player has any grenade activity at all
            if (stats.blindedEnemies == 0 && stats.blindedTeam == 0 && stats.blindedSelf == 0 &&
                stats.blindedByEnemies == 0 && stats.blindedByTeam == 0 &&
                stats.totalGrenadeDamageTakenFromEnemies == 0 && stats.totalGrenadeDamageTakenFromTeam == 0 && stats.totalGrenadeDamageTakenSelf == 0 &&
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

            // Self-damage taken (high priority - learn to avoid own grenades)
            if (stats.totalGrenadeDamageTakenSelf > 0)
            {
                negativeStats.Add((Localizer["grenade.stats.damage.received.self"].Value
                    .Replace("{damage}", stats.totalGrenadeDamageTakenSelf.ToString()), 86, stats.totalGrenadeDamageTakenSelf));
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
                    .Replace("{time}", stats.blindedByTotalAmount.ToString("0.0")), 60, (int)stats.blindedByTotalAmount));
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
                    .Replace("{time}", stats.blindedTotalAmount.ToString("0.0")), 80, (int)stats.blindedTotalAmount));
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

        private void PrintTopPlayersStats()
        {
            List<(CCSPlayerController player, string achievement, int score, string category)> playerAchievements = [];

            foreach ((CCSPlayerController? player, (int blindedEnemies, int blindedTeam, int blindedSelf, float blindedTotalAmount, int blindedByEnemies, int blindedByTeam, float blindedByTotalAmount, int TotalGrenadesThrown, int TotalGrenadeDamageTakenFromEnemies, int TotalGrenadeDamageTakenFromTeam, int TotalGrenadeDamageTakenSelf, int TotalGrenadeDamageGivenToEnemies, int TotalGrenadeDamageGivenToTeam, int totalGrenadeBounces) stats) in _players)
            {
                if (player == null || !player.IsValid || player.IsBot || player.IsHLTV)
                {
                    continue;
                }

                // Calculate different achievement scores for each player
                List<(string achievement, int score, string category)> achievements = [];

                // Positive achievements (higher is better)
                if (stats.blindedEnemies > 0)
                {
                    achievements.Add((Localizer["leaderboard.flashbang.enemies"].Value
                        .Replace("{count}", stats.blindedEnemies.ToString()),
                        (stats.blindedEnemies * 100) + (int)(stats.blindedTotalAmount * 10), "Flash Master"));
                }

                if (stats.TotalGrenadeDamageGivenToEnemies > 0)
                {
                    achievements.Add((Localizer["leaderboard.damage.enemies"].Value
                        .Replace("{damage}", stats.TotalGrenadeDamageGivenToEnemies.ToString()),
                        stats.TotalGrenadeDamageGivenToEnemies * 2, "Damage Dealer"));
                }

                if (stats.blindedTotalAmount > 5.0f)
                {
                    achievements.Add((Localizer["leaderboard.blind.time"].Value
                        .Replace("{time}", stats.blindedTotalAmount.ToString("0.0")),
                        (int)(stats.blindedTotalAmount * 20), "Blind Master"));
                }

                if (stats.totalGrenadeBounces > 0 && stats.blindedEnemies > 0)
                {
                    achievements.Add((Localizer["leaderboard.tactical.nades"].Value
                        .Replace("{bounces}", stats.totalGrenadeBounces.ToString())
                        .Replace("{flashes}", stats.blindedEnemies.ToString()),
                        (stats.totalGrenadeBounces * 30) + (stats.blindedEnemies * 20), "Tactical Genius"));
                }

                // Multi-skill achievements
                if (stats.TotalGrenadesThrown >= 3 && stats.blindedEnemies > 0 && stats.TotalGrenadeDamageGivenToEnemies > 0)
                {
                    // Combined impact score: flashes + damage with minimum activity
                    int combinedScore = (stats.blindedEnemies * 50) + stats.TotalGrenadeDamageGivenToEnemies;
                    achievements.Add((Localizer["leaderboard.versatile.player"].Value
                        .Replace("{flashes}", stats.blindedEnemies.ToString())
                        .Replace("{damage}", stats.TotalGrenadeDamageGivenToEnemies.ToString())
                        .Replace("{nades}", stats.TotalGrenadesThrown.ToString()),
                        combinedScore, "Versatile Player"));
                }

                // Specialist achievements for high single-stat performance
                if (stats.blindedEnemies >= 3 && stats.TotalGrenadeDamageGivenToEnemies < 30)
                {
                    // Pure flashbang specialist
                    achievements.Add((Localizer["leaderboard.flash.specialist"].Value
                        .Replace("{count}", stats.blindedEnemies.ToString())
                        .Replace("{time}", stats.blindedTotalAmount.ToString("0.0")),
                        (stats.blindedEnemies * 80) + (int)(stats.blindedTotalAmount * 10), "Flash Specialist"));
                }

                if (stats.TotalGrenadeDamageGivenToEnemies >= 60 && stats.blindedEnemies == 0)
                {
                    // Pure damage specialist
                    achievements.Add((Localizer["leaderboard.damage.specialist"].Value
                        .Replace("{damage}", stats.TotalGrenadeDamageGivenToEnemies.ToString()),
                        stats.TotalGrenadeDamageGivenToEnemies * 3, "Damage Specialist"));
                }

                // Anti-achievements (negative but notable)
                if (stats.blindedSelf > 0 || stats.blindedTeam > 0)
                {
                    int mistakes = (stats.blindedSelf * 2) + stats.blindedTeam;
                    if (mistakes >= 2)
                    {
                        achievements.Add((Localizer["leaderboard.team.flasher"].Value
                            .Replace("{self}", stats.blindedSelf.ToString())
                            .Replace("{team}", stats.blindedTeam.ToString()),
                            mistakes * 50, "Team Flasher"));
                    }
                }

                if (stats.TotalGrenadeDamageGivenToTeam > 30)
                {
                    achievements.Add((Localizer["leaderboard.team.damage"].Value
                        .Replace("{damage}", stats.TotalGrenadeDamageGivenToTeam.ToString()),
                        stats.TotalGrenadeDamageGivenToTeam, "Friendly Fire"));
                }

                // Get the best achievement for this player
                if (achievements.Count > 0)
                {
                    (string? achievement, int score, string? category) = achievements.OrderByDescending(static x => x.score).First();
                    playerAchievements.Add((player, achievement, score, category));
                }
            }

            // Get top 3 players
            List<(CCSPlayerController player, string achievement, int score, string category)> topPlayers = [.. playerAchievements
                .OrderByDescending(static x => x.score)
                .Take(3)];

            if (topPlayers.Count > 0)
            {
                // Send header message
                Server.PrintToChatAll(Localizer["leaderboard.header"].Value);

                // Display top players
                for (int i = 0; i < topPlayers.Count; i++)
                {
                    (CCSPlayerController? player, string? achievement, int score, string? category) = topPlayers[i];
                    string message = Localizer["leaderboard.player"].Value
                        .Replace("{medal}", $"#{i + 1}")
                        .Replace("{player}", player.PlayerName)
                        .Replace("{category}", category)
                        .Replace("{achievement}", achievement);

                    Server.PrintToChatAll(message);
                }
            }
        }
    }
}
