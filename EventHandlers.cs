using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using HintServiceMeow.Core.Enum;
using HintServiceMeow.Core.Extension;
using MEC;
using PlayerStatsSystem;
using MeowHint = HintServiceMeow.Core.Models.Hints.Hint;

namespace ScpSlDamageDisplay
{
    internal sealed class EventHandlers : IDisposable
    {
        private readonly DamageDisplayPlugin plugin;
        private readonly Dictionary<int, PendingDamage> pendingDamage = new Dictionary<int, PendingDamage>();
        private readonly Dictionary<int, VictimDamage> victimDamage = new Dictionary<int, VictimDamage>();
        private readonly Dictionary<int, AttackerDisplay> displays = new Dictionary<int, AttackerDisplay>();

        private long nextSequence;
        private long nextExpirationToken;

        public EventHandlers(DamageDisplayPlugin plugin)
        {
            this.plugin = plugin;
        }

        public void OnHurting(HurtingEventArgs ev)
        {
            if (ev == null || !ev.IsAllowed || !IsEnemyPlayerDamage(ev.Attacker, ev.Player, ev.DamageHandler.IsFriendlyFire))
            {
                if (ev?.Player != null)
                    pendingDamage.Remove(ev.Player.Id);

                return;
            }

            pendingDamage[ev.Player.Id] = new PendingDamage
            {
                Attacker = ev.Attacker,
                Target = ev.Player,
                Before = GetDamageSnapshot(ev.Player),
                ReportedDamage = ev.Amount,
            };
        }

        public void OnHurt(HurtEventArgs ev)
        {
            if (ev?.Player == null || !pendingDamage.TryGetValue(ev.Player.Id, out PendingDamage pending))
                return;

            pendingDamage.Remove(ev.Player.Id);
            StandardDamageHandler appliedDamage = ev.DamageHandler?.CustomBase?.As<StandardDamageHandler>();
            RecordActualDamage(pending, GetDamageSnapshot(ev.Player), false, ev.Amount, appliedDamage);
        }

        public void OnDied(DiedEventArgs ev)
        {
            if (ev?.Player == null)
                return;

            if (pendingDamage.TryGetValue(ev.Player.Id, out PendingDamage pending))
            {
                pendingDamage.Remove(ev.Player.Id);
                RecordActualDamage(pending, new DamageSnapshot(), true, pending.ReportedDamage, null);
            }

            ResolveDeath(ev.Player, ev.Attacker);
        }

        public void OnSpawned(SpawnedEventArgs ev)
        {
            // A living spawn starts a fresh life. Spectator spawning during the death
            // pipeline must not erase contributions before Died is raised.
            if (ev?.Player == null || !ev.Player.IsAlive)
                return;

            pendingDamage.Remove(ev.Player.Id);
            victimDamage.Remove(ev.Player.Id);
        }

        public void OnLeft(LeftEventArgs ev)
        {
            if (ev?.Player == null)
                return;

            int playerId = ev.Player.Id;
            pendingDamage.Remove(playerId);
            victimDamage.Remove(playerId);
            RemoveDisplay(playerId);

            foreach (VictimDamage victim in victimDamage.Values)
                victim.Contributions.Remove(playerId);
        }

        public void OnWaitingForPlayers()
        {
            ClearAll();
        }

        public void Dispose()
        {
            ClearAll();
        }

        private void RecordActualDamage(
            PendingDamage pending,
            DamageSnapshot after,
            bool targetDied,
            float reportedDamage,
            StandardDamageHandler appliedDamage)
        {
            if (pending?.Attacker == null || pending.Target == null || pending.Before == null || after == null)
                return;

            float beforeHealthPool = Math.Max(0f, pending.Before.Health) + Math.Max(0f, pending.Before.ArtificialHealth);
            float afterHealthPool = Math.Max(0f, after.Health) + Math.Max(0f, after.ArtificialHealth);
            float healthDamage = PositiveDelta(beforeHealthPool, afterHealthPool);
            float shieldDamage = PositiveDelta(pending.Before.HumeShield, after.HumeShield);

            // StandardDamageHandler records the result of this exact hit after
            // ApplyDamage. Prefer it over snapshots: on some server/game builds the
            // HumeShield property is not refreshed in time for EXILED's Hurt event.
            if (TryGetAppliedDamage(appliedDamage, out float appliedHealthDamage, out float appliedShieldDamage))
            {
                healthDamage = appliedHealthDamage;
                shieldDamage = appliedShieldDamage;
            }

            // Some EXILED/SCPSL combinations raise Hurt before HumeShield exposes its
            // refreshed value. Classify the unaccounted, reported damage as shield
            // damage while a shield was present so those hits are never invisible.
            if (pending.Before.HumeShield > 0.0001f && shieldDamage <= 0.0001f)
            {
                float safeReportedDamage = Math.Max(0f, reportedDamage);
                float unaccountedDamage = Math.Max(0f, safeReportedDamage - healthDamage);
                shieldDamage = Math.Min(Math.Max(0f, pending.Before.HumeShield), unaccountedDamage);
            }

            float actualDamage = shieldDamage + healthDamage;
            if (actualDamage <= 0.0001f || float.IsNaN(actualDamage) || float.IsInfinity(actualDamage))
                return;

            DateTime now = DateTime.UtcNow;
            int targetId = pending.Target.Id;
            int attackerId = pending.Attacker.Id;

            if (!victimDamage.TryGetValue(targetId, out VictimDamage victim))
            {
                victim = new VictimDamage
                {
                    VictimName = SafeName(pending.Target),
                };
                victimDamage.Add(targetId, victim);
            }

            if (!victim.Contributions.TryGetValue(attackerId, out Contribution contribution))
            {
                contribution = new Contribution
                {
                    Attacker = pending.Attacker,
                    AttackerName = SafeName(pending.Attacker),
                };
                victim.Contributions.Add(attackerId, contribution);
            }

            contribution.Attacker = pending.Attacker;
            contribution.AttackerName = SafeName(pending.Attacker);
            contribution.Damage += actualDamage;
            contribution.LastDamageUtc = now;

            if (!targetDied)
            {
                if (healthDamage > 0.0001f)
                {
                    if (contribution.DisplayPhase != DamageDisplayPhase.Health)
                    {
                        contribution.DisplayPhase = DamageDisplayPhase.Health;
                        contribution.HealthPhaseDamage = 0d;
                    }

                    contribution.HealthPhaseDamage += healthDamage;
                    ShowDamage(pending.Attacker, targetId, contribution.HealthPhaseDamage, plugin.Config.Color);
                }
                else if (shieldDamage > 0.0001f)
                {
                    if (contribution.DisplayPhase != DamageDisplayPhase.Shield)
                    {
                        contribution.DisplayPhase = DamageDisplayPhase.Shield;
                        contribution.ShieldPhaseDamage = 0d;
                    }

                    contribution.ShieldPhaseDamage += shieldDamage;
                    ShowDamage(pending.Attacker, targetId, contribution.ShieldPhaseDamage, plugin.Config.ShieldColor);
                }
            }

            if (plugin.Config.Debug)
                Log.Debug($"[ScpSlDamageDisplay] {contribution.AttackerName} -> {victim.VictimName}: 护盾 {shieldDamage:F2}, 真实 {healthDamage:F2} (总累计 {contribution.Damage:F2})");
        }

        private void ResolveDeath(Player victimPlayer, Player killer)
        {
            int victimId = victimPlayer.Id;
            if (!victimDamage.TryGetValue(victimId, out VictimDamage victim))
                return;

            victimDamage.Remove(victimId);

            DateTime now = DateTime.UtcNow;
            double assistWindow = Math.Max(0f, plugin.Config.AssistWindowSeconds);
            int killerId = killer?.Id ?? int.MinValue;

            List<Contribution> eligible = victim.Contributions.Values
                .Where(contribution => contribution.Damage > 0d)
                .Where(contribution => contribution.Attacker != null)
                .Where(contribution => contribution.Attacker.Id == killerId ||
                                       (now - contribution.LastDamageUtc).TotalSeconds <= assistWindow)
                .ToList();

            double totalDamage = eligible.Sum(contribution => contribution.Damage);
            if (totalDamage <= 0d)
                return;

            foreach (Contribution contribution in eligible)
            {
                bool isKiller = contribution.Attacker.Id == killerId;
                double percentage = contribution.Damage / totalDamage * 100d;
                string result = string.Format(
                    CultureInfo.InvariantCulture,
                    "💀 {0}：{1}，造成伤害：{2}（{3}%伤害贡献）",
                    isKiller ? "击杀" : "助攻",
                    EscapeRichText(victim.VictimName),
                    FormatNumber(contribution.Damage),
                    FormatNumber(percentage));

                ShowResult(contribution.Attacker, victimId, result);
            }
        }

        private void ShowDamage(Player attacker, int targetId, double damage, string color)
        {
            string text = WrapRichText(FormatNumber(damage), color);
            UpsertLine(attacker, targetId, text, plugin.Config.DamageDisplaySeconds);
        }

        private void ShowResult(Player attacker, int targetId, string result)
        {
            string text = WrapRichText(result, plugin.Config.ResultColor);
            UpsertLine(attacker, targetId, text, plugin.Config.ResultDisplaySeconds);
        }

        private void UpsertLine(Player player, int targetId, string text, float durationSeconds)
        {
            if (player == null)
                return;

            AttackerDisplay display = GetOrCreateDisplay(player);
            if (!display.Lines.TryGetValue(targetId, out DisplayLine line))
            {
                line = new DisplayLine
                {
                    Sequence = ++nextSequence,
                };
                display.Lines.Add(targetId, line);
            }

            line.Text = text;
            line.ExpirationToken = ++nextExpirationToken;
            long token = line.ExpirationToken;

            Render(display);

            float safeDuration = Math.Max(0.1f, durationSeconds);
            Timing.CallDelayed(safeDuration, () => ExpireLine(player.Id, targetId, token));
        }

        private AttackerDisplay GetOrCreateDisplay(Player player)
        {
            if (displays.TryGetValue(player.Id, out AttackerDisplay display))
            {
                display.Player = player;
                return display;
            }

            MeowHint hint = new MeowHint
            {
                Id = $"ScpSlDamageDisplay-{player.Id}",
                Alignment = HintAlignment.Center,
                XCoordinate = plugin.Config.XCoordinate,
                YCoordinate = plugin.Config.YCoordinate,
                YCoordinateAlign = HintVerticalAlign.Top,
                Text = string.Empty,
            };

            display = new AttackerDisplay
            {
                Player = player,
                Hint = hint,
            };

            displays.Add(player.Id, display);
            player.AddHint(hint);
            return display;
        }

        private void ExpireLine(int playerId, int targetId, long token)
        {
            if (!displays.TryGetValue(playerId, out AttackerDisplay display) ||
                !display.Lines.TryGetValue(targetId, out DisplayLine line) ||
                line.ExpirationToken != token)
            {
                return;
            }

            display.Lines.Remove(targetId);
            if (display.Lines.Count == 0)
            {
                RemoveDisplay(playerId);
                return;
            }

            Render(display);
        }

        private void Render(AttackerDisplay display)
        {
            display.Hint.Alignment = HintAlignment.Center;
            display.Hint.XCoordinate = plugin.Config.XCoordinate;
            display.Hint.YCoordinate = plugin.Config.YCoordinate;
            display.Hint.Text = string.Join(
                "\n",
                display.Lines.Values.OrderBy(line => line.Sequence).Select(line => line.Text));
        }

        private void RemoveDisplay(int playerId)
        {
            if (!displays.TryGetValue(playerId, out AttackerDisplay display))
                return;

            displays.Remove(playerId);
            try
            {
                display.Player?.RemoveHint(display.Hint);
            }
            catch (Exception exception)
            {
                if (plugin.Config.Debug)
                    Log.Debug($"[ScpSlDamageDisplay] 移除 HintServiceMeow 提示失败：{exception.Message}");
            }
        }

        private void ClearAll()
        {
            foreach (int playerId in displays.Keys.ToList())
                RemoveDisplay(playerId);

            pendingDamage.Clear();
            victimDamage.Clear();
            displays.Clear();
        }

        private string WrapRichText(string text, string configuredColor = null)
        {
            int fontSize = Math.Max(1, plugin.Config.FontSize);
            string colorSource = configuredColor ?? plugin.Config.Color;
            string color = string.IsNullOrWhiteSpace(colorSource) ? "#FFFFFF" : colorSource.Trim();
            return $"<b><color={color}><size={fontSize}>{text}</size></color></b>";
        }

        private string FormatNumber(double value)
        {
            int decimals = Math.Max(0, Math.Min(3, plugin.Config.DecimalPlaces));
            string format = decimals == 0 ? "0" : "0." + new string('#', decimals);
            return value.ToString(format, CultureInfo.InvariantCulture);
        }

        private static bool IsEnemyPlayerDamage(Player attacker, Player target, bool isFriendlyFire)
        {
            return attacker != null &&
                   target != null &&
                   attacker != target &&
                   attacker.Id != target.Id &&
                   !isFriendlyFire;
        }

        private static DamageSnapshot GetDamageSnapshot(Player player)
        {
            if (player == null)
                return new DamageSnapshot();

            return new DamageSnapshot
            {
                Health = player.Health,
                ArtificialHealth = player.ArtificialHealth,
                HumeShield = player.HumeShield,
            };
        }

        private static float PositiveDelta(float before, float after)
        {
            float delta = Math.Max(0f, before) - Math.Max(0f, after);
            return float.IsNaN(delta) || float.IsInfinity(delta) ? 0f : Math.Max(0f, delta);
        }

        private static bool TryGetAppliedDamage(
            StandardDamageHandler damageHandler,
            out float healthDamage,
            out float shieldDamage)
        {
            healthDamage = 0f;
            shieldDamage = 0f;

            if (damageHandler == null)
                return false;

            healthDamage = Math.Max(0f, damageHandler.DealtHealthDamage) +
                           Math.Max(0f, damageHandler.AbsorbedAhpDamage);
            shieldDamage = Math.Max(0f, damageHandler.AbsorbedHumeDamage);

            if (float.IsNaN(healthDamage) || float.IsInfinity(healthDamage) ||
                float.IsNaN(shieldDamage) || float.IsInfinity(shieldDamage))
            {
                healthDamage = 0f;
                shieldDamage = 0f;
                return false;
            }

            return healthDamage + shieldDamage > 0.0001f;
        }

        private static string SafeName(Player player)
        {
            return string.IsNullOrWhiteSpace(player?.Nickname) ? "未知玩家" : player.Nickname;
        }

        private static string EscapeRichText(string value)
        {
            return (value ?? string.Empty)
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");
        }
    }
}
