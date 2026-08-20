using System.Collections.Generic;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using Custom.NDMenuManager.Runtime;

namespace Custom.NDMenuManager.Editor.UI
{
    public class ParameterDetailInfo
    {
        public string Name;
        public string ValueType; // "Bool", "Int", "Float"
        public int BitCost; // 0, 1, 8
        public bool IsSynced;
        public bool IsSaved;
        public string DefaultValue;
        public string Category; // "基础参数", "ND 菜单控制项", "插件组件"
        public string SourcePath;
        public GameObject SourceGameObject;
        public Object SourceComponent;
        public bool IsOptimizable;
        public string OptimizationTip;
    }

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
        public List<ParameterDetailInfo> parameters;
    }

    public static class BitBudgetCalculator
    {
        public const int MaxBits = 256;

        public static BitBudgetResult Calculate(VRCAvatarDescriptor descriptor)
        {
            var parameterMap = new Dictionary<string, ParameterDetailInfo>();
            int baseBits = 0;

            if (descriptor != null)
            {
                // 1. Collect base avatar synced parameters (VRCExpressionParameters)
                if (descriptor.expressionParameters != null && descriptor.expressionParameters.parameters != null)
                {
                    foreach (var param in descriptor.expressionParameters.parameters)
                    {
                        if (param == null || string.IsNullOrEmpty(param.name)) continue;
                        int bits = param.networkSynced ? GetParamTypeBits(param.valueType) : 0;
                        if (param.networkSynced) baseBits += bits;

                        string defVal = param.valueType == VRCExpressionParameters.ValueType.Bool 
                            ? (param.defaultValue > 0.5f ? "True" : "False") 
                            : param.defaultValue.ToString("0.##");

                        parameterMap[param.name] = new ParameterDetailInfo
                        {
                            Name = param.name,
                            ValueType = param.valueType.ToString(),
                            BitCost = bits,
                            IsSynced = param.networkSynced,
                            IsSaved = param.saved,
                            DefaultValue = defVal,
                            Category = "Avatar 基础参数 (ExpressionParams)",
                            SourcePath = descriptor.gameObject.name,
                            SourceGameObject = descriptor.gameObject,
                            SourceComponent = descriptor.expressionParameters
                        };
                    }
                }

                // 2. Collect Modular NDMenu components
                var items = descriptor.GetComponentsInChildren<INDMenuItem>(true);
                foreach (var item in items)
                {
                    string pName = string.IsNullOrEmpty(item.ParameterName) ? item.MenuName : item.ParameterName;
                    if (string.IsNullOrEmpty(pName)) continue;

                    int cost = item.GetBitCost();
                    var mb = item as MonoBehaviour;
                    var go = mb != null ? mb.gameObject : null;
                    string path = go != null ? GetHierarchyPath(go) : descriptor.gameObject.name;

                    string valType = "Bool";
                    string defVal = item.DefaultValue.ToString();
                    bool optimizable = false;
                    string optTip = "";

                    if (item is NDToggleItem toggle)
                    {
                        valType = toggle.UseIntParameter ? "Int" : "Bool";
                        defVal = toggle.UseIntParameter ? toggle.ParameterValue.ToString() : (toggle.DefaultValue ? "True" : "False");
                        if (toggle.UseIntParameter && toggle.ParameterValue <= 2)
                        {
                            optimizable = true;
                            optTip = "仅有 2 项状态，可优化为 1-bit Bool (立省 7 bits)";
                        }
                    }
                    else if (item is NDRadialPuppet)
                    {
                        valType = "Float";
                    }

                    if (!parameterMap.ContainsKey(pName))
                    {
                        parameterMap[pName] = new ParameterDetailInfo
                        {
                            Name = pName,
                            ValueType = valType,
                            BitCost = cost,
                            IsSynced = item.Synced,
                            IsSaved = item.Saved,
                            DefaultValue = defVal,
                            Category = "ND 菜单控制项",
                            SourcePath = path,
                            SourceGameObject = go,
                            SourceComponent = mb,
                            IsOptimizable = optimizable,
                            OptimizationTip = optTip
                        };
                    }
                    else
                    {
                        // Update if this item consumes more bits or adds info
                        var existing = parameterMap[pName];
                        if (cost > existing.BitCost) existing.BitCost = cost;
                        if (existing.Category.Contains("基础参数"))
                        {
                            existing.Category += " + ND菜单关联";
                        }
                    }
                }
            }

            var paramList = new List<ParameterDetailInfo>(parameterMap.Values);

            int totalUsed = 0;
            foreach (var p in paramList)
            {
                if (p.IsSynced) totalUsed += p.BitCost;
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
                uniqueParamCount = paramList.Count,
                percentage = pct,
                isExceeded = exceeded,
                statusText = status,
                barColor = color,
                parameters = paramList
            };
        }

        private static string GetHierarchyPath(GameObject go)
        {
            var parts = new List<string>();
            var curr = go.transform;
            while (curr != null)
            {
                parts.Insert(0, curr.name);
                curr = curr.parent;
            }
            return string.Join("/", parts);
        }

        private static int GetParamTypeBits(VRCExpressionParameters.ValueType type)
        {
            switch (type)
            {
                case VRCExpressionParameters.ValueType.Bool:
                    return 1;
                case VRCExpressionParameters.ValueType.Int:
                case VRCExpressionParameters.ValueType.Float:
                    return 8;
                default:
                    return 0;
            }
        }
    }
}
