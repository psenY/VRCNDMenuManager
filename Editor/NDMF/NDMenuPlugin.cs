using nadena.dev.ndmf;
using Custom.NDMenuManager.Editor.NDMF;

[assembly: ExportsPlugin(typeof(NDMenuPlugin))]

namespace Custom.NDMenuManager.Editor.NDMF
{
    public class NDMenuPlugin : Plugin<NDMenuPlugin>
    {
        public override string QualifiedName => "com.custom.nd-menu-manager";
        public override string DisplayName => "psenY7 ND Menu Manager";

        protected override void Configure()
        {
            InPhase(BuildPhase.Generating)
                .Run("Generate ND Menus & FX Layers", ctx =>
                {
                    NDMenuBuildPass.Execute(ctx);
                });
        }
    }
}
