using TheArchive.Core;
using TheArchive.Core.Attributes;
using TheArchive.Core.FeaturesAPI;
using TheArchive.Core.Localization;

namespace Hikaria.CGHUDInfo
{
    [ArchiveModule(PluginInfo.GUID, PluginInfo.NAME, PluginInfo.VERSION)]
	public class EntryPoint : IArchiveModule
    {
        public void Init()
		{
            Logs.LogMessage("OK");
		}

        public void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
        }

        public void OnLateUpdate()
        {
        }

        public void OnExit()
        {
        }
        public bool ApplyHarmonyPatches => false;

        public bool UsesLegacyPatches => false;

        public ArchiveLegacyPatcher Patcher { get; set; }

        public string ModuleGroup => FeatureGroups.GetOrCreateModuleGroup("CGHUDInfo", new()
        {
            { Language.English, "Color Grading HUD Info" },
            { Language.Chinese, "颜色分级 HUD 信息" }
        });
    }
}
