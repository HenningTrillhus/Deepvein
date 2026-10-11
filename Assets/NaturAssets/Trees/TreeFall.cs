using System.Collections;
using UnityEngine;

/// <summary>
/// Det som skjer med et tre når det hugges:
///   - hvert treff: treet skjelver (kronen pisker frem og tilbake og roer seg), via TreeSway.Shake
///   - siste treff: treet faller bort fra spilleren (roterer rundt foten), blekner ut og forsvinner
///   - en stubbe blir stående igjen på stedet (stubbebildet velges etter treets navn, se Rules)
/// Legges på spilleren (samme objekt som TreeChopper). Settes opp av menyen DeepVain > Trees > 5 - Set up tree falling.
/// </summary>
[DisallowMultipleComponent]
public class TreeFall : MonoBehaviour
{
    [System.Serializable]
    public class StumpRule
    {
        [Tooltip("Del av navnet på treets bilde, f.eks. OldOak eller Pine. Første regel som passer vinner.")]
        public string nameContains;
        public Sprite stump;
    }

    [Header("Treff (skjelving)")]
    [Tooltip("Hvor kraftig treet skjelver (1 = normalt).")]
    public float shakeStrength = 1f;
    [Tooltip("Hvor lenge skjelvingen varer (sekunder).")]
    public float shakeDuration = 0.45f;

    [Header("Fall")]
    [Tooltip("Hvor lang tid det tar å falle (sekunder). Starter sakte og tar fart.")]
    public float fallDuration = 0.9f;
    [Tooltip("Hvor langt det faller (grader). 90 = helt ned på bakken.")]
    public float fallAngle = 88f;
    [Tooltip("Lite sprett når det treffer bakken (grader).")]
    public float bounceAngle = 5f;
    [Tooltip("Hvor lenge det ligger før det blekner, og hvor lang tid bleknet tar.")]
    public float lieTime = 0.35f;
    public float fadeDuration = 0.7f;
    [Tooltip("Faller bort fra spilleren. Av = faller tilfeldig vei.")]
    public bool fallAwayFromPlayer = true;

    [Header("Stubbe")]
    [Tooltip("Brukes når ingen av reglene passer.")]
    public Sprite defaultStump;
    public StumpRule[] rules;
    [Tooltip("Flytter stubben opp/ned i forhold til treets fot (units).")]
    public float stumpYOffset = 0f;

    TreeChopper chopper;

    void Awake() { chopper = GetComponent<TreeChopper>(); }

    void OnEnable()
    {
        if (chopper == null) chopper = GetComponent<TreeChopper>();
        if (chopper != null) { chopper.TreeHit += OnHit; chopper.TreeFelled += OnFelled; }
    }

    void OnDisable()
    {
        if (chopper != null) { chopper.TreeHit -= OnHit; chopper.TreeFelled -= OnFelled; }
    }

    void OnHit(GameObject tree, float hpLeft)
    {
        if (tree == null) return;
        var sway = tree.GetComponent<TreeSway>();
        if (sway != null) { sway.Shake(shakeStrength, shakeDuration); return; }
        StartCoroutine(JitterRoutine(tree.transform));           // a tree without TreeSway: a small sideways jitter instead
    }

    IEnumerator JitterRoutine(Transform t)
    {
        if (t == null) yield break;
        Vector3 p0 = t.position; float el = 0f;
        while (el < shakeDuration && t != null)
        {
            el += Time.deltaTime;
            float k = 1f - el / shakeDuration;
            t.position = p0 + new Vector3(Mathf.Round(Mathf.Sin(el * 70f) * k * 2f) * 0.04f, 0f, 0f);
            yield return null;
        }
        if (t != null) t.position = p0;
    }

    void OnFelled(GameObject tree)
    {
        if (tree != null) StartCoroutine(FallRoutine(tree));
    }

    Sprite PickStump(Sprite treeSprite)
    {
        string n = treeSprite != null ? treeSprite.name : "";
        if (rules != null)
            foreach (var r in rules)
                if (r != null && r.stump != null && !string.IsNullOrEmpty(r.nameContains) && n.IndexOf(r.nameContains, System.StringComparison.OrdinalIgnoreCase) >= 0) return r.stump;
        return defaultStump;
    }

    IEnumerator FallRoutine(GameObject tree)
    {
        TreeHover.Exclude(tree);
        var t = tree.transform;
        var sr = tree.GetComponentInChildren<SpriteRenderer>();
        var sway = tree.GetComponent<TreeSway>();
        if (sway != null) sway.Still(true);
        foreach (var c in tree.GetComponentsInChildren<Collider2D>()) c.enabled = false;

        // which way: away from the player
        int dir = 1;
        if (fallAwayFromPlayer) dir = t.position.x >= transform.position.x ? 1 : -1;
        else dir = Random.value < 0.5f ? -1 : 1;

        SpawnStump(tree, sr);

        Quaternion q0 = t.rotation;
        float el = 0f;
        while (el < fallDuration && t != null)
        {
            el += Time.deltaTime;
            float p = Mathf.Clamp01(el / fallDuration);
            t.rotation = q0 * Quaternion.Euler(0f, 0f, -dir * fallAngle * p * p);       // slow start, then it takes speed
            yield return null;
        }
        // small bounce when it hits the ground
        float bt = 0f, bd = 0.22f;
        while (bt < bd && t != null)
        {
            bt += Time.deltaTime;
            float p = Mathf.Clamp01(bt / bd);
            float a = fallAngle - bounceAngle * Mathf.Sin(p * Mathf.PI);
            t.rotation = q0 * Quaternion.Euler(0f, 0f, -dir * a);
            yield return null;
        }
        if (t == null) yield break;
        yield return new WaitForSeconds(lieTime);

        // fade away
        float fe = 0f;
        Color c0 = sr != null ? sr.color : Color.white;
        while (fe < fadeDuration && tree != null)
        {
            fe += Time.deltaTime;
            if (sr != null) sr.color = new Color(c0.r, c0.g, c0.b, 1f - Mathf.Clamp01(fe / fadeDuration));
            yield return null;
        }
        if (tree != null) Destroy(tree);
    }

    void SpawnStump(GameObject tree, SpriteRenderer treeSr)
    {
        Sprite stump = PickStump(treeSr != null ? treeSr.sprite : null);
        if (stump == null) return;

        var go = new GameObject("Stump_" + (treeSr != null && treeSr.sprite != null ? treeSr.sprite.name : tree.name));
        go.transform.SetParent(tree.transform.parent, false);
        go.transform.position = tree.transform.position + new Vector3(0f, stumpYOffset, 0f);
        go.transform.rotation = Quaternion.identity;
        go.transform.localScale = tree.transform.localScale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = stump;
        if (treeSr != null)
        {
            sr.sortingLayerID = treeSr.sortingLayerID;
            sr.sortingOrder = treeSr.sortingOrder - 1;                 // just behind the falling trunk
            sr.flipX = treeSr.flipX;
        }
        go.AddComponent<StumpMarker>();
    }
}

/// <summary>Merker en stubbe som er laget av TreeFall (så den kan finnes igjen, f.eks. for å la et tre gro på nytt).</summary>
public class StumpMarker : MonoBehaviour { }
