using UnityEngine;

/// <summary>Spiller en rekke bilder i løkke på en SpriteRenderer (flagg, flammer, alt som bare skal gå rundt). Hvert objekt starter på et tilfeldig bilde.</summary>
[DisallowMultipleComponent]
public class SpriteLoop : MonoBehaviour
{
    public Sprite[] frames;
    [Tooltip("Bilder per sekund (8 = 120 ms per bilde, som de andre animasjonene).")]
    public float fps = 8f;
    [Tooltip("Start på et tilfeldig bilde, så flere flagg ikke går i takt.")]
    public bool randomStart = true;

    SpriteRenderer sr;
    float t;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (randomStart && frames != null && frames.Length > 0) t = Random.value * frames.Length / Mathf.Max(0.01f, fps);
    }

    void Update()
    {
        if (sr == null || frames == null || frames.Length == 0) return;
        t += Time.deltaTime;
        int i = Mathf.FloorToInt(t * Mathf.Max(0.01f, fps)) % frames.Length;
        sr.sprite = frames[i];
    }
}
