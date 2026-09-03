// ================================================================
// 决竞球 CLEAN_v2 合法骨架: FieldEffectZone.cs [BattleBall.Battle.Physics]
// 语义逻辑: Step A~F 大类统一核对; [TODO Step-*] 处待后续补全
// ================================================================
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;
using BattleBall.Core;

namespace BattleBall.Battle.Physics
{

    public class FieldEffectZone : MonoBehaviour
    {
        public enum ZoneType { Normal, OutOfBounds, GoalA, GoalB, ElementField, Slow, Boost }
        public ZoneType zoneType = ZoneType.Normal;
        public string element = ""; public float factor = 1.0f;
        public Bounds bounds; protected virtual void Awake() { bounds = new Bounds(transform.position, Vector3.one); }
        public virtual bool Contains(Vector3 p) => bounds.Contains(p);
        public virtual void OnPlayerEnter(Transform p) { }
        public virtual void OnPlayerExit (Transform p) { }
    }

}
