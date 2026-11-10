using UnityEngine;

namespace Majungbul
{
    /// <summary>
    /// 프로토타입용 절차적 효과음. 실제 녹음/에셋이 확보되면 AudioClip 필드로 교체한다.
    /// 필수 효과음 5종: 컵 마찰음, 사진 받침음, 풍경 종소리, 점화음, 소화음.
    /// </summary>
    public static class Sfx
    {
        const int Rate = 44100;

        static AudioClip _cup, _photo, _chime, _ignite, _douse;

        public static AudioClip Cup => _cup ??= Make("cup", 0.18f, (t, i) =>
            Mathf.Sin(2f * Mathf.PI * 1650f * t) * Decay(t, 0.002f, 0.05f) * 0.35f
            + Noise() * Decay(t, 0.001f, 0.03f) * 0.25f);

        public static AudioClip Photo => _photo ??= Make("photo", 0.2f, (t, i) =>
            Mathf.Sin(2f * Mathf.PI * 140f * t) * Decay(t, 0.003f, 0.06f) * 0.6f
            + Noise() * Decay(t, 0.001f, 0.015f) * 0.2f);

        public static AudioClip Chime => _chime ??= Make("chime", 2.2f, (t, i) =>
            (Mathf.Sin(2f * Mathf.PI * 1318f * t) * 0.5f
           + Mathf.Sin(2f * Mathf.PI * 2637f * t) * 0.2f
           + Mathf.Sin(2f * Mathf.PI * 3951f * t) * 0.1f) * Decay(t, 0.004f, 0.7f) * 0.45f);

        public static AudioClip Ignite => _ignite ??= MakeNoise("ignite", 0.8f, rising: true);
        public static AudioClip Douse => _douse ??= MakeNoise("douse", 0.9f, rising: false);

        static float Decay(float t, float attack, float tau)
        {
            if (t < attack) return t / attack;
            return Mathf.Exp(-(t - attack) / tau);
        }

        static float Noise() => Random.value * 2f - 1f;

        static AudioClip Make(string name, float seconds, System.Func<float, int, float> f)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f(i / (float)Rate, i), -1f, 1f);
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // 1차 저역통과 노이즈. rising=true면 점점 커지는 점화음, false면 '훅' 꺼지는 소화음.
        static AudioClip MakeNoise(string name, float seconds, bool rising)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            float y = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float p = t / seconds;
                float cutoff = rising ? Mathf.Lerp(0.02f, 0.25f, p) : Mathf.Lerp(0.3f, 0.01f, p);
                y += cutoff * (Noise() - y);
                float env = rising ? Mathf.Sin(Mathf.PI * p) * Mathf.Sqrt(p + 0.05f)
                                   : Decay(t, 0.01f, 0.18f);
                data[i] = Mathf.Clamp(y * env * 2.2f, -1f, 1f);
            }
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
