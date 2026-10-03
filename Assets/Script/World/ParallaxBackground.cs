using UnityEngine;

public class ParallaxBackground : MonoBehaviour
{
    [Header("Parallax")]
    [Tooltip("0 = følger kameraet helt (uendelig langt unna). 1 = står stille som vanlig terreng.")]
    [Range(0f, 1f)]
    [SerializeField] private float parallaxFactor = 0.9f;
    [Tooltip("La bakgrunnen følge kameraet vertikalt også.")]
    [SerializeField] private bool followVertical = true;

    [Header("Uendelig scroll")]
    [Tooltip("Gjenta bakgrunnen sidelengs så den aldri tar slutt.")]
    [SerializeField] private bool infiniteHorizontal = true;

    private Transform cam;
    private Vector3 lastCamPos;
    private float textureWidth;

    void Start()
    {
        cam = Camera.main.transform;
        lastCamPos = cam.position;

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null)
            textureWidth = sr.bounds.size.x;
    }

    void LateUpdate()
    {
        Vector3 delta = cam.position - lastCamPos;

        // Flytt mindre enn kameraet, så den ser ut til å ligge langt unna
        transform.position += new Vector3(
            delta.x * (1f - parallaxFactor),
            followVertical ? delta.y * (1f - parallaxFactor) : 0f,
            0f);

        lastCamPos = cam.position;

        // Hopp en bildebredde når kameraet har passert kanten
        if (infiniteHorizontal && textureWidth > 0f)
        {
            float distance = cam.position.x * parallaxFactor;

            if (distance > transform.position.x + textureWidth)
                transform.position += new Vector3(textureWidth, 0f, 0f);
            else if (distance < transform.position.x - textureWidth)
                transform.position -= new Vector3(textureWidth, 0f, 0f);
        }
    }
}