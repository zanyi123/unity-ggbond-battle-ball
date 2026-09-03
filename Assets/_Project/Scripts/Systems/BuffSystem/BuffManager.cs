using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using BattleBall.Core;
using BattleBall.Battle;

namespace BattleBall.Systems.Buff
{
    public class BuffManager : MonoBehaviour
    {
        public Dictionary<int, HashSet<string>> layerToTagIds = new Dictionary<int, HashSet<string>>();
        public Dictionary<string, Dictionary<string, object>> tagRegistry = new Dictionary<string, Dictionary<string, object>>();

        public static BuffManager Instance { get; private set; }

        protected virtual void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            for (int i = 0; i < 6; i++) layerToTagIds[i] = new HashSet<string>();
        }
        protected virtual void OnDestroy() { if (Instance == this) Instance = null; }

        protected virtual void Start()
        {
            var dm = DataManager.Instance;
            if (dm != null) {
                dm.data_loaded += OnDataLoaded;
                // 防止Start比DM.Start晚（已加载的情况）立即调用
                if (dm.tags != null && dm.tags.Count > 0) OnDataLoaded();
            }
        }
        private bool _dataLoaded = false;
        protected virtual void OnDataLoaded()
        {
            if (_dataLoaded) return;
            var dm = DataManager.Instance;
            if (dm == null) return;
            RegisterTags(dm.tags);
            _dataLoaded = true;
            int total = 0;
            foreach (var kv in layerToTagIds) total += kv.Value.Count;
            Debug.Log("[BuffMgr] 6层标签就绪: totalTags=" + total + " registry=" + tagRegistry.Count + " elems=" + dm.elements.Count);
        }

        public virtual void RegisterTags(List<Dictionary<string, object>> tags)
        {
            if (tags == null) return;
            foreach (var t in tags)
            {
                if (!t.ContainsKey("id")) continue;
                string id = t["id"].ToString();
                tagRegistry[id] = t;
                int layer = 0;
                if (t.ContainsKey("tag_layer")) layer = Convert.ToInt32(Convert.ToDouble(t["tag_layer"]));
                else if (t.ContainsKey("layer")) layer = Convert.ToInt32(Convert.ToDouble(t["layer"]));
                layer = Mathf.Clamp(layer, 0, 5);
                if (!layerToTagIds.ContainsKey(layer)) layerToTagIds[layer] = new HashSet<string>();
                layerToTagIds[layer].Add(id);
            }
        }

        public virtual Dictionary<string, object> GetTag(string id)
        {
            if (string.IsNullOrEmpty(id) || !tagRegistry.ContainsKey(id)) return new Dictionary<string, object>();
            return tagRegistry[id];
        }

        public virtual void ApplyTag(PlayerController pc, string tagId, float overrideDuration = -1f)
        {
            if (pc == null || string.IsNullOrEmpty(tagId)) return;
            var t = GetTag(tagId);
            if (t.Count == 0) return;
            float duration = overrideDuration;
            if (duration < 0f)
            {
                if (t.ContainsKey("duration")) duration = (float)Convert.ToDouble(t["duration"]);
                else duration = 3f;
            }
            float magnitude = 0f;
            if (t.ContainsKey("magnitude")) magnitude = (float)Convert.ToDouble(t["magnitude"]);
            string type = t.ContainsKey("type") ? t["type"].ToString().ToLower() : "buff";
            switch (type)
            {
                case "status": case "stun": case "root": case "stagger":
                    pc.AddStatus(tagId, duration); break;
                default:
                    pc.AddBuff(tagId, magnitude, duration); break;
            }
        }
    }
}
