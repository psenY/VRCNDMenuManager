using System.Collections.Generic;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using Custom.NDMenuManager.Runtime;

namespace Custom.NDMenuManager.Editor.UI
{
    public struct BitBudgetResult
    {
        public int baseBits;
        public int addedBits;
        public int totalUsedBits;
        public int maxBits;
        public int uniqueParamCount;
        public float percentage;
        public bool isExceeded;
        public string statusText;
        public Color barColor;
    }

    public static class BitBudgetCalculator
    {
        public const int MaxBits = 256;

        public static BitBudgetResult Calculate(VRCAvatarDescriptor descriptor)
        {
            var parameterMap = new Dictionary<string, int>(); // paramName -> bitCost
            int baseBits = 0;

            if (descriptor != null)
            {
                // 1. Collect base avatar synced parameters (Contacts, OSC, Gestures, MA items)
                if (descriptor.expressionParameters != null && descriptor.expressionParameters.parameters != null)
                {
                    foreach (var param in descriptor.expressionParameters.parameters)
                    {
                        if (param == null || string.IsNullOrEmpty(param.name) || !param.networkSynced) continue;
                        int bits = GetParamTypeBits(param.valueType);
                        parameterMap[param.name] = bits;
                        baseBits += bits;
                    }
                }

                // 2. Merge modular NDMenu components (deduplicating by name)
                var items = descriptor.GetComponentsInChildren<INDMenuItem>(true);
                foreach (var item in items)
                {
                    if (!item.Synced) continue;
                    string pName = string.IsNullOrEmpty(item.ParameterName) ? item.MenuName : item.ParameterName;
                    if (string.IsNullOrEmpty(pName)) continue;

                    int cost = item.GetBitCost();
                    if (cost > 0)
                    {
                        if (!parameterMap.ContainsKey(pName) || parameterMap[pName] < cost)
                        {
                            parameterMap[pName] = cost;
                        }
                    }
                }
            }

            int totalUsed = 0;
            foreach (var kvp in parameterMap)
            {
                totalUsed += kvp.Value;
            }

            int addedBits = Mathf.Max(0, totalUsed - baseBits);
            float pct = Mathf.Clamp01((float)totalUsed / MaxBits);
            bool exceeded = totalUsed > MaxBits;

            Color color;
            string status;

            if (exceeded)
            {
                color = new Color(0.95f, 0.26f, 0.21f); // Red
                status = $"{totalUsed} / {MaxBits} bits (基础 {baseBits}b + 扩展 {addedBits}b) - 超出 {totalUsed - MaxBits} bits (若安装 VRCFury 将在上传时自动压缩)";
            }
            else if (totalUsed > 200)
            {
                color = new Color(1.0f, 0.7f, 0.0f); // Yellow / Amber
                status = $"{totalUsed} / {MaxBits} bits (基础 {baseBits}b + 扩展 {addedBits}b) - 剩余 {MaxBits - totalUsed} bits (接近上限)";
            }
            else
            {
                color = new Color(0.24f, 0.70f, 0.44f); // Emerald Green
                status = $"{totalUsed} / {MaxBits} bits (基础 {baseBits}b + 扩展 {addedBits}b) - 剩余 {MaxBits - totalUsed} bits";
            }

            return new BitBudgetResult
            {
                baseBits = baseBits,
                addedBits = addedBits,
                totalUsedBits = totalUsed,
                maxBits = MaxBits,
                uniqueParamCount = parameterMap.Count,
                percentage = pct,
                isExceeded = exceeded,
                statusText = status,
                barColor = color
            };
        }

        private static int GetParamTypeBits(VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.ValueType type)
        {
            switch (type)
            {
                case VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.ValueType.Bool:
                    return 1;
                case VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.ValueType.Int:
                case VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.ValueType.Float:
                    return 8;
                default:
                    return 0;
            }
        }
    }
}
