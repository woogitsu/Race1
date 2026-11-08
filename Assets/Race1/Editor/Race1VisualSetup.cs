using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Race1.Editor
{
    /// <summary>
    /// Writes the URP post-processing profile that the runtime PostProcessingController loads:
    /// Assets/Race1/Resources/Race1PostProfile.asset. The values match the controller's day-time defaults.
    /// <see cref="CreateProfileIfMissing"/> is safe to call from Race1BuildTool.SetupProject.
    /// </summary>
    public static class Race1VisualSetup
    {
        private const string ResourcesFolder = "Assets/Race1/Resources";
        private const string ProfilePath = ResourcesFolder + "/Race1PostProfile.asset";

        /// <summary>Menu entry. Asks before replacing an existing profile.</summary>
        [MenuItem("Race1/Create Visual Profile")]
        public static void CreateProfile()
        {
            if (AssetDatabase.LoadMainAssetAtPath(ProfilePath) != null)
            {
                bool replace = EditorUtility.DisplayDialog(
                    "Race1 visual profile",
                    "Race1PostProfile.asset already exists. Replace it with the default look?",
                    "Replace",
                    "Cancel");
                if (!replace)
                {
                    return;
                }

                AssetDatabase.DeleteAsset(ProfilePath);
            }

            CreateDefaultProfile();
        }

        /// <summary>Creates the default profile only when the asset is missing. Never overwrites an existing asset.</summary>
        public static void CreateProfileIfMissing()
        {
            if (AssetDatabase.LoadMainAssetAtPath(ProfilePath) != null)
            {
                return;
            }

            CreateDefaultProfile();
        }

        private static void CreateDefaultProfile()
        {
            EnsureResourcesFolder();

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Race1PostProfile";
            AssetDatabase.CreateAsset(profile, ProfilePath);

            var bloom = AddVolumeComponent<Bloom>(profile);
            bloom.intensity.value = 0.15f;

            var tonemapping = AddVolumeComponent<Tonemapping>(profile);
            tonemapping.mode.value = TonemappingMode.ACES;

            var colorAdjustments = AddVolumeComponent<ColorAdjustments>(profile);
            colorAdjustments.postExposure.value = 0.2f;
            colorAdjustments.contrast.value = 10f;
            colorAdjustments.saturation.value = 8f;
            colorAdjustments.colorFilter.value = Color.white;

            var vignette = AddVolumeComponent<Vignette>(profile);
            vignette.intensity.value = 0.22f;
            vignette.smoothness.value = 0.4f;

            var filmGrain = AddVolumeComponent<FilmGrain>(profile);
            filmGrain.intensity.value = 0.08f;

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            Debug.Log("Race1: created " + ProfilePath + ". Manual steps still needed: enable Screen Space " +
                      "Ambient Occlusion by adding its Renderer Feature to the URP renderer asset, and enable " +
                      "Post Processing in the URP asset and on the main camera.");
        }

        private static T AddVolumeComponent<T>(VolumeProfile profile) where T : VolumeComponent
        {
            T component = profile.Add<T>(true);
            if (string.IsNullOrEmpty(component.name))
            {
                component.name = typeof(T).Name;
            }

            if (!AssetDatabase.Contains(component))
            {
                AssetDatabase.AddObjectToAsset(component, profile);
            }

            EditorUtility.SetDirty(component);
            return component;
        }

        private static void EnsureResourcesFolder()
        {
            if (!Directory.Exists(ResourcesFolder))
            {
                Directory.CreateDirectory(ResourcesFolder);
            }

            if (!AssetDatabase.IsValidFolder(ResourcesFolder))
            {
                AssetDatabase.Refresh();
            }
        }
    }
}
