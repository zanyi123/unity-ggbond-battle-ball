using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleBall.Battle.AI
{
    public static class AiProfile
    {
        public class Profile
        {
            public string roleName;
            public float chaseWeight;
            public float shootWeight;
            public float passWeight;
            public float supportWeight;
            public float coverWeight;
            public float shootDist;
            public float tooFarFromHome;
            public float chaseDist;
            public float separation;
            public int decisionIntervalMs;
            public float moveSpeedMult;
            public float ball_pickup_range;
            // SpiritAIManager skill fields
            public float skillThinkInterval = 2f;
            public float skillEnergyMin = 30f;
            public float skillUseThreshold = 40f;
            public float skillSelectionTemperature = 0.5f;
            public float skillMistakeChance = 0.05f;
            public float skillAttackIntentWeight = 1f;
            public float skillDefenseIntentWeight = 0.8f;
            public float skillSupportIntentWeight = 0.6f;
            public float skillLateGameBonus = 1.3f;
            public float skillReserveWeight = 0.3f;
            public float skillExpectedFutureScore = 50f;
            public float skillUncertaintyDiscount = 0.2f;
            public float skillOutnumberedBonus = 1.2f;
            public float skillLosingBonus = 1.2f;
            public float skillLeadingPenalty = 0.8f;
            public string role = "attacker";
        }

        public static readonly Profile Attacker = new Profile {
            roleName = "Attacker",
            chaseWeight = 1.0f, shootWeight = 1.1f, passWeight = 0.5f,
            supportWeight = 0.4f, coverWeight = 0.3f,
            shootDist = 3.2f, tooFarFromHome = 6f, chaseDist = 5f,
            separation = 1.2f, decisionIntervalMs = 200,
            moveSpeedMult = 1.05f, ball_pickup_range = 0.45f,
        };

        public static readonly Profile Defender = new Profile {
            roleName = "Defender",
            chaseWeight = 0.7f, shootWeight = 0.6f, passWeight = 0.7f,
            supportWeight = 0.6f, coverWeight = 1.2f,
            shootDist = 2.0f, tooFarFromHome = 4f, chaseDist = 3.5f,
            separation = 1.4f, decisionIntervalMs = 260,
            moveSpeedMult = 0.95f, ball_pickup_range = 0.5f,
        };

        public static readonly Profile Support = new Profile {
            roleName = "Support",
            chaseWeight = 0.85f, shootWeight = 0.55f, passWeight = 1.3f,
            supportWeight = 1.2f, coverWeight = 0.7f,
            shootDist = 2.5f, tooFarFromHome = 5f, chaseDist = 4.2f,
            separation = 1.3f, decisionIntervalMs = 240,
            moveSpeedMult = 1.00f, ball_pickup_range = 0.5f,
        };

        public static Profile ByRole(string role)
        {
            var r = (role ?? "").ToLower();
            if (r.Contains("att") || r.Contains("pf") || r.Contains("sg")) return Attacker;
            if (r.Contains("def") || r.Contains("c")  || r.Contains("sf")) return Defender;
            if (r.Contains("sup") || r.Contains("pg")) return Support;
            return Attacker;
        }

        public static Profile ByIndex(int idx, string roleHint = "")
        {
            if (!string.IsNullOrEmpty(roleHint)) return ByRole(roleHint);
            switch (idx % 3) { case 0: return Attacker; case 1: return Defender; default: return Support; }
        }
    }
}
