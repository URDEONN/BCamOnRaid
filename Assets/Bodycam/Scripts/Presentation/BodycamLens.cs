using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BodycamRaid
{
    /// <summary>Uses the standard URP volume stack; F2 compares clean and bodycam views.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class BodycamLens : MonoBehaviour
    {
        public bool effectsEnabled = true;
        Volume volume;
        VolumeProfile ownedProfile;
        GameObject volumeObject;
        UniversalAdditionalCameraData cameraData;

        void Awake()
        {
            cameraData = GetComponent<Camera>().GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            // Explicitly include Default even when the camera/player uses Ignore Raycast.
            cameraData.volumeLayerMask = cameraData.volumeLayerMask.value | 1;
            volumeObject = new GameObject("Bodycam lens volume");
            volumeObject.transform.SetParent(transform, false);
            volumeObject.layer = 0;
            volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 30;
            ownedProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            ownedProfile.name = "Runtime bodycam profile";
            volume.sharedProfile = ownedProfile;

            var lens = ownedProfile.Add<LensDistortion>(true);
            lens.intensity.Override(-.12f);
            lens.scale.Override(1.025f);
            var vignette = ownedProfile.Add<Vignette>(true);
            vignette.intensity.Override(.28f);
            vignette.smoothness.Override(.75f);
            var grain = ownedProfile.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(.12f);
            grain.response.Override(.78f);
            var chromatic = ownedProfile.Add<ChromaticAberration>(true);
            chromatic.intensity.Override(.08f);
            var color = ownedProfile.Add<ColorAdjustments>(true);
            color.contrast.Override(9);
            color.saturation.Override(-12);
            var bloom = ownedProfile.Add<Bloom>(true);
            bloom.intensity.Override(.12f);
            bloom.threshold.Override(1.15f);
            ApplyState();
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F2))
            {
                effectsEnabled = !effectsEnabled;
                ApplyState();
            }
        }

        void ApplyState()
        {
            if (volume != null) volume.weight = effectsEnabled ? 1 : 0;
        }

        void OnDestroy()
        {
            if (volumeObject != null) Destroy(volumeObject);
            if (ownedProfile == null) return;
            foreach (VolumeComponent component in ownedProfile.components)
                if (component != null) Destroy(component);
            Destroy(ownedProfile);
        }
    }
}
