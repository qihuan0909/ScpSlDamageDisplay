using System;
using Exiled.API.Features;
using PlayerEvents = Exiled.Events.Handlers.Player;
using ServerEvents = Exiled.Events.Handlers.Server;

namespace ScpSlDamageDisplay
{
    public sealed class DamageDisplayPlugin : Plugin<Config>
    {
        public override string Name => "ScpSlDamageDisplay";

        public override string Prefix => "scp_sl_damage_display";

        public override string Author => "欢歌小鱼xCodex";

        public override Version Version { get; } = new Version(1, 2, 0);

        public override Version RequiredExiledVersion { get; } = new Version(9, 6, 0);

        internal EventHandlers Handlers { get; private set; }

        public override void OnEnabled()
        {
            Handlers = new EventHandlers(this);

            PlayerEvents.Hurting += Handlers.OnHurting;
            PlayerEvents.Hurt += Handlers.OnHurt;
            PlayerEvents.Died += Handlers.OnDied;
            PlayerEvents.Spawned += Handlers.OnSpawned;
            PlayerEvents.Left += Handlers.OnLeft;
            ServerEvents.WaitingForPlayers += Handlers.OnWaitingForPlayers;

            Log.Info($"{Name} v{Version} 已启用；伤害与击杀提示由 HintServiceMeow 管理。");
            base.OnEnabled();
        }

        public override void OnDisabled()
        {
            PlayerEvents.Hurting -= Handlers.OnHurting;
            PlayerEvents.Hurt -= Handlers.OnHurt;
            PlayerEvents.Died -= Handlers.OnDied;
            PlayerEvents.Spawned -= Handlers.OnSpawned;
            PlayerEvents.Left -= Handlers.OnLeft;
            ServerEvents.WaitingForPlayers -= Handlers.OnWaitingForPlayers;

            Handlers.Dispose();
            Handlers = null;

            base.OnDisabled();
        }
    }
}
