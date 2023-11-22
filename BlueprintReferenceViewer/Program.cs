using BlueprintReferenceViewer.BlueprintGeneratedClass;
using BlueprintReferenceViewer.Components;
using BlueprintReferenceViewer.Utils;
using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Versions;
using CUE4Parse.Utils;
using UAssetAPI;
using UAssetAPI.UnrealTypes;

namespace BlueprintReferenceViewer
{
    public static class Settings
    {
        public static string AssetsDirectory = @"C:\Users\Oleg\Desktop\dumper";
        public static string GameDirectory = @"C:\Program Files (x86)\Steam\steamapps\common\Dead by Daylight\DeadByDaylight\Content\Paks";
        public static string ProjectDirectory = @"C:\Users\Oleg\Desktop\OldTiles";

        public static VersionContainer CUE4Parse_GameVersion = new VersionContainer(EGame.GAME_UE4_27);
        public static EngineVersion UAssetAPI_GameVersion = EngineVersion.VER_UE4_27;

        public static bool bScanProjectForReferencedAssets = true;
        public static bool bIgnoreExistingAssetsAtPath = true;

        public static string[] IgnoreExistingAssetsAtPath = {  /// used only if bScanProjectForReferencedAssets and bIgnoreExistingAssetsAtPath are set to True
            "/Game/OriginalTiles",
            //"/Game/NewTiles",
            "/Game/MergedTiles",
        };
    }

    public class Program
    {
        public static UAsset? Asset = null;
        public static DefaultFileProvider? Provider = null;

        /** pairs AssetName - PackagePath (i.e. SM_Mesh - /Game/Meshes/SM_Mesh) */
        public static Dictionary<string, string> AlreadyExistingAssets = new Dictionary<string, string>();

        static void Main(string[] args)
        {
            Provider = new DefaultFileProvider(
                Settings.GameDirectory,
                SearchOption.TopDirectoryOnly,
                true,
                Settings.CUE4Parse_GameVersion
            );
            Provider.Initialize();
            Provider.Mount();

            if (Settings.bScanProjectForReferencedAssets)
                GetProjectAssets(ref AlreadyExistingAssets);

            // Clean up all existing "Level <num>" folders first
            string[] Subdirectories = Directory.GetDirectories(Settings.AssetsDirectory ?? "");
            foreach (string Subdirectory in Subdirectories)
                if (Subdirectory.ToLower().Contains("Level"))
                    Directory.Delete(Subdirectory, true);

            DirectoryInfo di = new DirectoryInfo(Settings.AssetsDirectory ?? "");
            FileInfo[] UAssetFiles = di.GetFiles("*.uasset");

            // Backup all files in AssetsDirectory folder so we can get back to it when we generated all referenced blueprints in Edtior
            if (!Directory.Exists($"{Settings.AssetsDirectory}\\Initial Blueprints"))
            {
                Directory.CreateDirectory($"{Settings.AssetsDirectory}\\Initial Blueprints");
                foreach (FileInfo file in di.GetFiles())
                    file.CopyTo($"{Settings.AssetsDirectory}\\Initial Blueprints\\{file.Name}");
            }

            foreach (FileInfo Uasset in UAssetFiles)
            {
                Asset = new UAsset(Uasset.FullName, Settings.UAssetAPI_GameVersion);
                ListReferencedBlueprints();

                Console.WriteLine();
            }
        }

        static void ListReferencedBlueprints(int NestingLevel = 0)
        {
            if (Asset is null)
                throw new Exception("Asset was null when tried to list referenced blueprints");

            UBlueprintGeneratedClass BPGC = new UBlueprintGeneratedClass(Asset.GetClassExport());

            if (NestingLevel != 0 && !BPGC.HasAnyComponents())
            {
                /** Clean up extracted assets since we are not interested in them */

                File.Delete(Asset.FilePath);
                File.Delete(Asset.FilePath.SubstringBeforeLast('.') + ".uexp");

                return;
            }

            for (int i = 0; i < NestingLevel; i++)
                Console.Write('\t');

            string AssetName = Path.GetFileNameWithoutExtension(Asset.FilePath);
            if (AlreadyExistingAssets.ContainsKey(AssetName))
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;

                if (NestingLevel > 0)
                {
                    foreach (string FilePath in Directory.GetFiles($"{Settings.AssetsDirectory}\\level {NestingLevel - 1}", $"{AssetName}.*"))
                        File.Delete(FilePath);

                    if (Directory.GetFiles($"{Settings.AssetsDirectory}\\level {NestingLevel - 1}").Length == 0)
                        Directory.Delete($"{Settings.AssetsDirectory}\\level {NestingLevel - 1}");
                }
            }

            Console.WriteLine(AssetName);
            Console.ForegroundColor = ConsoleColor.Gray;

            HashSet<string> BlueprintPackages = new HashSet<string>();

            List<UChildActorComponent> ChildActorComponents = BPGC.GetComponentsOfClass<UChildActorComponent>();
            foreach (UChildActorComponent ChildActorComponent in ChildActorComponents)
            {
                if (ChildActorComponent.ChildActorClass is not null)
                    BlueprintPackages.Add(ChildActorComponent.ChildActorClass);
            }

            // Get blueprints used as Visualization in ActorSpawners

            List<UActorSpawner> ActorSpawners = BPGC.GetComponentsOfClass<UActorSpawner>();
            foreach (UActorSpawner ActorSpawner in ActorSpawners)
            {
                if (ActorSpawner.Visualization is not null)
                    BlueprintPackages.Add(ActorSpawner.Visualization);
            }

            foreach (string BlueprintPackage in BlueprintPackages)
            {
                // Convert "/Game/..." into "DeadByDaylight/Content/..."
                string FModelPackagePath = "DeadByDaylight/Content/" + BlueprintPackage.SubstringAfter("/Game/");
                string ExportDirectory = $"{Settings.AssetsDirectory}\\Level {NestingLevel}";

                // Sanity check
                if (Provider is null) throw new Exception("Provider was null when tried to list referenced blueprints");
                if (!Provider.IsBlueprintChildOfAActor(FModelPackagePath))
                    continue;

                string? ExtractedBlueprintFilePath = Provider?.ExtractAsset(FModelPackagePath, ExportDirectory);

                if (ExtractedBlueprintFilePath is null)
                    continue;

                // Once we exported .uasset and .uexp from game files, open it with UAssetAPI
                Asset = null;
                try
                {
                    Asset = new UAsset(ExtractedBlueprintFilePath, Settings.UAssetAPI_GameVersion);
                }
                catch (Exception) { }

                ListReferencedBlueprints(NestingLevel + 1);
            }
        }

        static void GetProjectAssets(ref Dictionary<string, string> OutAlreadyExistingAssets)
        {
            if (!Directory.Exists(Settings.ProjectDirectory))
                throw new Exception("Project directory doesn't exist. Uncheck bScanProjectForReferencedAssets");

            string[] ProjectAssets = Directory.GetFiles($"{Settings.ProjectDirectory}\\Content", "*.uasset", SearchOption.AllDirectories);

            foreach (string projectAssetPath in ProjectAssets)
            {
                string AssetPath = "/Game" + projectAssetPath.SubstringAfter("Content").SubstringBeforeLast('.').Replace('\\', '/');

                bool bIncludeAsset = true;
                if (Settings.bIgnoreExistingAssetsAtPath)
                {
                    foreach (string IgnorePath in Settings.IgnoreExistingAssetsAtPath)
                        if (AssetPath.StartsWith(IgnorePath))
                        {
                            bIncludeAsset = false;
                            break;
                        }
                }

                if (bIncludeAsset)
                {
                    if (AlreadyExistingAssets.ContainsKey(projectAssetPath.GetAssetName()))
                        throw new Exception($"Two assets with the same name: {OutAlreadyExistingAssets[projectAssetPath.GetAssetName()]} and {AssetPath}");

                    OutAlreadyExistingAssets.Add(projectAssetPath.GetAssetName(), AssetPath);
                }
            }
        }
    }
}
