using System;
using System.Collections.Generic;
using Exiled.API.Features;
using MeowHint = HintServiceMeow.Core.Models.Hints.Hint;

namespace ScpSlDamageDisplay
{
    internal enum DamageDisplayPhase
    {
        None,
        Shield,
        Health,
    }

    internal sealed class DamageSnapshot
    {
        public float Health { get; set; }

        public float ArtificialHealth { get; set; }

        public float HumeShield { get; set; }
    }

    internal sealed class PendingDamage
    {
        public Player Attacker { get; set; }

        public Player Target { get; set; }

        public DamageSnapshot Before { get; set; }

        public float ReportedDamage { get; set; }
    }

    internal sealed class Contribution
    {
        public Player Attacker { get; set; }

        public string AttackerName { get; set; }

        public double Damage { get; set; }

        public DamageDisplayPhase DisplayPhase { get; set; }

        public double ShieldPhaseDamage { get; set; }

        public double HealthPhaseDamage { get; set; }

        public DateTime LastDamageUtc { get; set; }
    }

    internal sealed class VictimDamage
    {
        public string VictimName { get; set; }

        public Dictionary<int, Contribution> Contributions { get; } = new Dictionary<int, Contribution>();
    }

    internal sealed class DisplayLine
    {
        public long Sequence { get; set; }

        public long ExpirationToken { get; set; }

        public string Text { get; set; }
    }

    internal sealed class AttackerDisplay
    {
        public Player Player { get; set; }

        public MeowHint Hint { get; set; }

        public Dictionary<int, DisplayLine> Lines { get; } = new Dictionary<int, DisplayLine>();
    }
}
