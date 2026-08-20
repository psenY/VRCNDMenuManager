using UnityEngine;
using VRC.SDKBase;

namespace Custom.NDMenuManager.Runtime
{
    public interface INDMenuItem : IEditorOnly
    {
        string MenuName { get; set; }
        Texture2D Icon { get; set; }
        string ParameterName { get; set; }
        bool Saved { get; set; }
        bool Synced { get; set; }
        MenuItemType ItemType { get; }
        int GetBitCost();
    }
}
