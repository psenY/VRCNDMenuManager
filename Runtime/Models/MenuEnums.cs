using System;

namespace Custom.NDMenuManager.Runtime
{
    public enum MenuItemType
    {
        Toggle,
        ToggleGroup,
        RadialPuppet,
        SubMenu,
        Button
    }

    public enum TargetActionType
    {
        GameObjectActive,
        BlendShape,
        MaterialFloat,
        MaterialColor
    }

    public enum ParameterSyncMode
    {
        Synced,
        LocalOnly
    }
}
