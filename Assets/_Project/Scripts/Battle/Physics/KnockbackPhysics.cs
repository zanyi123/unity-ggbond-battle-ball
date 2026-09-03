// ================================================================
// 决竞球 CLEAN_v2: KnockbackPhysics.cs [BattleBall.Battle.Physics]
// 击退距离/时间计算模块 (注意 Godot px 已 ×0.01 → Unity m)
// ================================================================
using UnityEngine;
using System.Collections.Generic;
using System;

namespace BattleBall.Battle.Physics
{
    public class KnockbackPhysics : MonoBehaviour
    {
        // 基准 (原 Godot 值 100/200 px -> 1.00/2.00 m)
        public const float BASE_DISTANCE_K1   = 1.00f;
        public const float BASE_DISTANCE_K2   = 2.00f;
        public const float BALL_SPEED_NORMAL  = 4.00f;   // 4 m/s
        public const float FRICTION_NORMAL    = 1.0f;
        public const float FRICTION_ICE       = 0.5f;
        public const float FRICTION_MUD       = 1.5f;

        /// <summary>d_final = (d_baseline / mu) * skill_mult + offset, 球速加成可选</summary>
        public static float calculate_distance(
            string knockback_type = "knockback1",
            float friction_coefficient = 1.0f,
            float skill_distance_multiplier = 1.0f,
            float skill_distance_offset   = 0.0f,
            float ball_speed = 4.0f,
            bool  enable_ball_speed_bonus  = false)
        {
            float base_dist = (knockback_type == "knockback1") ? BASE_DISTANCE_K1 : BASE_DISTANCE_K2;
            if (friction_coefficient <= 0f) friction_coefficient = 0.01f;
            float distance = base_dist / friction_coefficient;
            distance *= skill_distance_multiplier;
            distance += skill_distance_offset;
            if (enable_ball_speed_bonus && BALL_SPEED_NORMAL > 0f)
                distance *= (ball_speed / BALL_SPEED_NORMAL);
            return distance;
        }

        /// <summary>击退持续时间 = 僵直(秒), 可选摩擦修正</summary>
        public static float calculate_duration(float stun_time_sec, float friction = 1.0f)
        {
            if (friction <= 0f) friction = 0.01f;
            return stun_time_sec * Mathf.Clamp01(1f / friction);
        }
    }
}
