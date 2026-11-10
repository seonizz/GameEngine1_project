using System;
using UnityEngine;

namespace Majungbul.House
{
    /// <summary>
    /// 프로토타입용 절차적 소리. 실제 녹음·에셋이 확보되면 AudioClip 필드로 교체한다.
    /// 모두 22.05kHz 모노. 처음 요청할 때 한 번 만들고 재사용한다.
    /// </summary>
    public static class HouseAudio
    {
        const int Rate = 22050;
        static readonly System.Random Rng = new System.Random(7);
        static AudioClip _crickets, _knock, _clatter, _ring, _static, _chime, _water, _wind;

        static float Noise() => (float)Rng.NextDouble() * 2f - 1f;
        static float Env(float t, float attack, float tau) => t < attack ? t / attack : Mathf.Exp(-(t - attack) / tau);

        static AudioClip Make(string name, float seconds, Func<float, float> f)
        {
            int n = (int)(Rate * seconds);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f(i / (float)Rate), -1f, 1f);
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>여름밤 벌레 소리(반복용 6초).</summary>
        public static AudioClip Crickets => _crickets ?? (_crickets = Make("crickets", 6f, t =>
            Chirp(t, 0.95f, 4300f, 0.16f) + Chirp(t + 0.4f, 1.35f, 3800f, 0.09f) + Noise() * 0.006f));

        /// <summary>나무 문·창을 세 번 두드리는 소리.</summary>
        public static AudioClip Knock => _knock ?? (_knock = Make("knock", 1.3f, t =>
        {
            float s = 0f;
            for (int k = 0; k < 3; k++)
            {
                float u = t - k * 0.38f;
                if (u >= 0f) s += (Mathf.Sin(2f * Mathf.PI * 95f * u) * 0.8f + Noise() * 0.3f) * Env(u, 0.002f, 0.045f);
            }
            return s * 0.8f;
        }));

        public static AudioClip Clatter => _clatter ?? (_clatter = Make("clatter", 0.5f, t =>
            (Noise() * 0.5f + Mathf.Sin(2f * Mathf.PI * 820f * t) * 0.3f) * Env(t, 0.002f, 0.08f)
            + Noise() * 0.3f * Env(Mathf.Max(0f, t - 0.15f), 0.002f, 0.05f) * (t > 0.15f ? 1f : 0f)));

        public static AudioClip Ring => _ring ?? (_ring = Make("ring", 1.6f, t =>
        {
            bool on = (t % 0.8f) < 0.5f;
            float tone = Mathf.Sin(2f * Mathf.PI * 440f * t) + Mathf.Sin(2f * Mathf.PI * 480f * t);
            return on ? tone * 0.18f * (Mathf.Sin(2f * Mathf.PI * 20f * t) > 0f ? 1f : 0.3f) : 0f;
        }));

        public static AudioClip Static => _static ?? (_static = Make("static", 1.4f, t =>
            Noise() * 0.22f * Env(t, 0.05f, 0.9f) + Mathf.Sin(2f * Mathf.PI * 330f * t) * 0.05f * Env(t, 0.2f, 0.4f)));

        public static AudioClip Chime => _chime ?? (_chime = Make("chime", 2.2f, t =>
            (Mathf.Sin(2f * Mathf.PI * 523f * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * 1046f * t) * 0.2f) * Env(t, 0.004f, 0.7f) * 0.4f));

        public static AudioClip Water => _water ?? (_water = Make("water", 0.9f, t =>
            Mathf.Sin(2f * Mathf.PI * (600f + 900f * Env(t, 0.001f, 0.06f)) * t) * Env(t, 0.003f, 0.12f) * 0.3f + Noise() * 0.05f * Env(t, 0.01f, 0.3f)));

        public static AudioClip Wind => _wind ?? (_wind = Make("wind", 2.0f, t =>
        {
            float k = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 2f));
            return Noise() * 0.18f * k;
        }));

        public static AudioClip For(HouseNoise n)
        {
            switch (n)
            {
                case HouseNoise.Clatter: return Clatter;
                case HouseNoise.Ring: return Ring;
                case HouseNoise.Static: return Static;
                case HouseNoise.Chime: return Chime;
                case HouseNoise.Water: return Water;
                case HouseNoise.Knock: return Knock;
                default: return null;
            }
        }

        // 주기 period마다 짧은 울음 3번(각 0.05초, 0.11초 간격).
        static float Chirp(float t, float period, float freq, float amp)
        {
            float local = t % period;
            for (int k = 0; k < 3; k++)
            {
                float u = local - k * 0.11f;
                if (u >= 0f && u < 0.05f)
                    return Mathf.Sin(Mathf.PI * u / 0.05f) * Mathf.Sin(2f * Mathf.PI * freq * t) * amp;
            }
            return 0f;
        }
    }
}
