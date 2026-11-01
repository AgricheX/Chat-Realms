using System;
using NeoModLoader.api;

namespace ChatRealms
{
    internal class Settings
    {
        private readonly ModConfig _cfg;

        public Settings(ModConfig cfg)
        {
            _cfg = cfg;
        }

        private ModConfigItem Item(string group, string id)
        {
            return _cfg[group][id];
        }

        public bool TwitchEnabled { get { return Item("ChatRealms", "Twitch Enabled").BoolVal; } }
        public string TwitchChannel { get { return (Item("ChatRealms", "Twitch Channel").TextVal ?? "").Trim(); } }
        public bool Roleplay { get { return Item("ChatRealms", "Roleplay Mode").BoolVal; } }
        public string RoleplayTarget { get { return (Item("ChatRealms", "Roleplay Target").TextVal ?? "").Trim(); } }
        public int PollDuration { get { return Math.Max(5, Item("ChatRealms", "Poll Duration").IntVal); } }
        public int PollInterval { get { return Math.Max(5, Item("ChatRealms", "Poll Interval").IntVal); } }
        public int PollOptions { get { return Math.Max(2, Math.Min(6, Item("ChatRealms", "Poll Options").IntVal)); } }
        public bool AllowDestructive { get { return Item("ChatRealms", "Allow Destructive Events").BoolVal; } }
        public bool ShowOverlay { get { return Item("ChatRealms", "Show Overlay").BoolVal; } }
        public bool Portuguese { get { return Item("ChatRealms", "Use Portuguese").BoolVal; } }

        public bool RelayEnabled { get { return Item("Discord", "Relay Enabled").BoolVal; } }
        public string RelayUrl { get { return (Item("Discord", "Relay URL").TextVal ?? "").Trim(); } }
        public string PairingCode { get { return (Item("Discord", "Pairing Code").TextVal ?? "").Trim(); } }
        public bool UnitMode { get { return Item("Discord", "Unit Mode").BoolVal; } }
        public bool AllowWars { get { return Item("Discord", "Allow Discord Wars").BoolVal; } }
        public bool AllowDiscordDestructive { get { return Item("Discord", "Allow Discord Destructive").BoolVal; } }
        public int UserCooldown { get { return Math.Max(1, Item("Discord", "User Cooldown").IntVal); } }
        public int ActionsPerMinute { get { return Math.Max(1, Item("Discord", "Actions Per Minute").IntVal); } }
        public string BlockedWords { get { return Item("Discord", "Blocked Words").TextVal ?? ""; } }
        public bool AllowScreenshots { get { return Item("Discord", "Allow Screenshots").BoolVal; } }
        public bool HideUiInShots { get { return Item("Discord", "Hide UI In Screenshots").BoolVal; } }
        public int WarRumorYears { get { return Math.Max(0, Item("Discord", "War Rumor Years").IntVal); } }
        public bool ApplyRules { get { return Item("Discord", "Apply RP Rules").BoolVal; } }
        public bool CohesionEnabled { get { return Item("Discord", "Cohesion Consequences").BoolVal; } }
        public bool InsecureTls { get { return Item("Discord", "Allow Insecure TLS").BoolVal; } }
    }
}
