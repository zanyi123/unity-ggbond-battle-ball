// ================================================================
// 决竞球 CLEAN_v2 合法骨架: FieldZone.cs [BattleBall.Battle.Physics]
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

    public class FieldZone : MonoBehaviour
    {
        public enum Side { LeftTeamB, RightTeamA, InnerField }
        public Side side = Side.InnerField;
        public Rect bodyRect   = new Rect(3.80f, -3.25f, 1.30f, 6.50f);
        public Rect armTopRect = new Rect(2.50f,  2.60f, 1.30f, 0.65f);
        public Rect armBotRect = new Rect(2.50f, -3.25f, 1.30f, 0.65f);
        public virtual bool Contains(Vector3 p) { var r = new Vector2(p.x, p.z); return bodyRect.Contains(r) || armTopRect.Contains(r) || armBotRect.Contains(r); }
    }

}
