using System;
using UnityEngine;

namespace BodycamRaid
{
    // Synthetic placeholder audio. No external samples or licensed assets.
    public sealed class RaidAudio : MonoBehaviour
    {
        AudioSource source;
        AudioClip step, cloth, landing;
        int count;
        void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.spatialBlend = 0f;
            source.playOnAwake = false;
            source.volume = 0.36f;
            step = Make("Concrete boot", 0.14f, 95f, 101);
            cloth = Make("Equipment movement", 0.11f, 180f, 202);
            landing = Make("Landing", 0.22f, 60f, 303);
        }
        static AudioClip Make(string name, float length, float frequency, int seed)
        {
            const int rate = 22050;
            float[] data = new float[(int)(length * rate)];
            var rng = new System.Random(seed);
            float filtered = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)rate;
                float noise = (float)rng.NextDouble() * 2f - 1f;
                filtered = Mathf.Lerp(filtered, noise, 0.15f);
                float envelope = Mathf.Exp(-t / length * 7f) * Mathf.Min(1f, t * 1200f);
                data[i] = (filtered * 0.65f + Mathf.Sin(t * frequency * Mathf.PI * 2f) * 0.35f) * envelope;
            }
            var clip = AudioClip.Create(name, data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
        public void Step(float volume)
        {
            source.pitch = (++count % 2 == 0) ? 0.92f : 1.07f;
            source.PlayOneShot(step, volume);
        }
        public void Jump() { source.pitch = 1f; source.PlayOneShot(cloth, 0.35f); }
        public void Land(float strength) { source.pitch = 0.9f; source.PlayOneShot(landing, Mathf.Lerp(0.3f, 1f, strength)); }
        void OnDestroy() { Destroy(step); Destroy(cloth); Destroy(landing); }
    }
}
