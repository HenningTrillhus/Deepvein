using UnityEngine;

/// <summary>Spiller av en rekke bilder på en SpriteRenderer én gang (røyken når en stein knuses) og fjerner objektet til slutt.</summary>
public class SpriteFlipbook : MonoBehaviour
{
    public Sprite[] frames;
    public float frameTime = 0.07f;
    public bool destroyAtEnd = true;

    SpriteRenderer sr;
    float t;

    void Awake() { sr = GetComponent<SpriteRenderer>(); }

    void Update()
    {
        if (frames == null || frames.Length == 0 || sr == null) return;
        t += Time.deltaTime;
        int i = Mathf.FloorToInt(t / Mathf.Max(0.01f, frameTime));
        if (i >= frames.Length)
        {
            if (destroyAtEnd) Destroy(gameObject); else sr.enabled = false;
            return;
        }
        sr.sprite = frames[i];
    }
}
