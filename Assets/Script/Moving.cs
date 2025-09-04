using UnityEngine;

public class Moving : MonoBehaviour
{
    [Header("Layout (GameManager에서 SetLayout으로 주입됨)")]
    [SerializeField] Vector2 cellSize = new(1.2f, 1.2f);
    [SerializeField] Vector2 originOffset = new(-1.8f, -1.8f);

    [Header("Motion")]
    [SerializeField] float durationPerCell = 0.08f;
    [Range(0f, 1f)] [SerializeField] float ease = 0.8f;

    bool hasTarget;
    bool combineOnArrive;
    Vector3 startPos, targetPos;
    float elapsed, duration;

    void Update()
    {
        if (!hasTarget) return;

        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);

        if (ease > 0f)
        {
            t = Mathf.SmoothStep(0f, 1f, t);
            if (ease > 0.5f) t = Mathf.SmoothStep(0f, 1f, t);
        }

        transform.position = Vector3.Lerp(startPos, targetPos, t);

        if (t >= 1f)
        {
            hasTarget = false;
            if (combineOnArrive)
            {
                combineOnArrive = false;
                Destroy(gameObject);
            }
        }
    }

    public void Move(int x2, int y2, bool combine)
    {
        combineOnArrive = combine;

        startPos = transform.position;
        targetPos = new Vector3(
            originOffset.x + cellSize.x * x2,
            originOffset.y + cellSize.y * y2,
            0f
        );

        int cx = Mathf.RoundToInt((startPos.x - originOffset.x) / cellSize.x);
        int cy = Mathf.RoundToInt((startPos.y - originOffset.y) / cellSize.y);
        int cells = Mathf.Abs(x2 - cx) + Mathf.Abs(y2 - cy);
        cells = Mathf.Max(1, cells);

        duration = durationPerCell * cells;
        elapsed = 0f;
        hasTarget = true;
    }

    public void SetLayout(Vector2 cellSize, Vector2 originOffset)
    {
        this.cellSize = cellSize;
        this.originOffset = originOffset;
    }
}
