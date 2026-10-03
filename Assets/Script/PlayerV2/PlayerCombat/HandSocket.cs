using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using DeepVain.Player;

/// <summary>
/// An empty object that follows the player's hand in every animation frame (position + forearm direction),
/// read from Anchors_Hands.csv. Put the sword (or a tool / shield) as a CHILD of this object.
/// Put this object under the same object that gets flipped (it is flipped automatically with the player).
/// </summary>
[DefaultExecutionOrder(200)]
public class HandSocket : MonoBehaviour
{
    public enum Hand { Front, Back }

    [Header("Source")]
    [SerializeField] private PlayerLayerSync sync;
    [Tooltip("Drag Anchors_Hands.csv here (it imports as a Text Asset).")]
    [SerializeField] private TextAsset anchorsCsv;
    [Tooltip("Front = the arm in front of the body (the one that holds the sword).")]
    public Hand hand = Hand.Front;

    [Header("Scale (must match the sprites)")]
    [Tooltip("Same Pixels Per Unit as you used in DeepVain > Player Setup.")]
    public float pixelsPerUnit = 40f;
    [Tooltip("x of the feet centre in the 24-wide frames. Do not change.")]
    public float standX = 12f;

    [Header("Fine tuning")]
    [Tooltip("Rotate the held item relative to the forearm (degrees). Try 0, 90, -90, 45 until the sword points the right way.")]
    public float rotationOffset = 0f;
    [Tooltip("Move the grip a little (units), in the unflipped player space.")]
    public Vector2 localOffset = Vector2.zero;
    [Tooltip("Hide the children (the sword) in animations without a hand position (DeathFade).")]
    [SerializeField] private bool hideWhenNoAnchor = true;

    struct Anchor { public float x, y, angle; public int xOffset, canvasH; }
    readonly Dictionary<string, Anchor> table = new Dictionary<string, Anchor>();
    bool childrenShown = true;

    void Awake()
    {
        if (sync == null) sync = GetComponentInParent<PlayerLayerSync>();
        if (sync == null) sync = GetComponentInChildren<PlayerLayerSync>();
        Parse();
    }

    void Parse()
    {
        table.Clear();
        if (anchorsCsv == null) { Debug.LogWarning("HandSocket: Anchors_Hands.csv is not assigned."); return; }
        var lines = anchorsCsv.text.Split('\n');
        for (int i = 1; i < lines.Length; i++)
        {
            var f = lines[i].Trim().Split(',');
            if (f.Length < 9) continue;
            var inv = CultureInfo.InvariantCulture;
            var a = new Anchor
            {
                x = float.Parse(f[3], inv), y = float.Parse(f[4], inv), angle = float.Parse(f[5], inv),
                xOffset = int.Parse(f[7], inv), canvasH = int.Parse(f[8], inv)
            };
            table[f[0] + "|" + f[1] + "|" + f[2]] = a;
        }
    }

    void LateUpdate()
    {
        if (sync == null) return;
        string key = sync.CurrentAnimation + "|" + (sync.CurrentFrame + 1) + "|" + (hand == Hand.Front ? "ArmFront" : "ArmBack");

        if (!table.TryGetValue(key, out var a))
        {
            if (hideWhenNoAnchor) ShowChildren(false);
            return;
        }
        ShowChildren(true);

        // pixel (x right, y down, origin top-left of the frame) -> unit offset from the feet pivot
        float px = (a.x - (standX + a.xOffset)) / pixelsPerUnit;
        float py = ((a.canvasH - 1) - a.y) / pixelsPerUnit;
        transform.localPosition = new Vector3(px + localOffset.x, py + localOffset.y, transform.localPosition.z);

        // forearm angle (0 = pointing down, 90 = pointing forward) -> direction in unflipped player space
        float rad = a.angle * Mathf.Deg2Rad;
        float deg = Mathf.Atan2(-Mathf.Cos(rad), Mathf.Sin(rad)) * Mathf.Rad2Deg;
        transform.localRotation = Quaternion.Euler(0f, 0f, deg + rotationOffset);
    }

    void ShowChildren(bool on)
    {
        if (childrenShown == on) return;
        childrenShown = on;
        foreach (Transform c in transform) c.gameObject.SetActive(on);
    }
}
