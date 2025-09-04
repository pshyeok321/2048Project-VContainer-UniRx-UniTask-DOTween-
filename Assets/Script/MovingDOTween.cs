using UnityEngine;
using DG.Tweening;

public class MovingDOTween : MonoBehaviour
{
    [Header("Layout (GameManager/TileManager에서 SetLayout으로 주입)")]
    [SerializeField] Vector2 cellSize = new(1.2f, 1.2f);
    [SerializeField] Vector2 originOffset = new(-1.8f, -1.8f);

    [Header("Motion")]
    [SerializeField] float durationPerCell = 0.08f;
    [SerializeField] Ease ease = Ease.OutQuad;
    [SerializeField] bool useUnscaledTime = false; // 일시정지 대응 원하면 true

    Tween activeTween;

    public void SetLayout(Vector2 cellSize, Vector2 originOffset)
    {
        this.cellSize = cellSize;
        this.originOffset = originOffset;
    }

    public void Move(int x2, int y2, bool combine)
    {
        // 타깃 좌표
        Vector3 targetPos = new(
            originOffset.x + cellSize.x * x2,
            originOffset.y + cellSize.y * y2,
            0f
        );

        // 현재 셀 좌표로 이동거리(맨해튼) 계산 → 시간 산정
        Vector3 startPos = transform.position;
        int cx = Mathf.RoundToInt((startPos.x - originOffset.x) / cellSize.x);
        int cy = Mathf.RoundToInt((startPos.y - originOffset.y) / cellSize.y);
        int cells = Mathf.Abs(x2 - cx) + Mathf.Abs(y2 - cy);
        if (cells < 1) cells = 1;

        float duration = durationPerCell * cells;

        // 이전 트윈 정리 후 재생
        KillActive(false);
        activeTween = transform.DOMove(targetPos, duration)
            .SetEase(ease)
            .SetUpdate(useUnscaledTime)
            .OnComplete(() =>
            {
                activeTween = null;
                if (combine) Destroy(gameObject);
            });
    }

    void OnDisable() => KillActive(false);
    void OnDestroy() => KillActive(false);

    void KillActive(bool complete)
    {
        if (activeTween != null && activeTween.IsActive())
        {
            activeTween.Kill(complete);
            activeTween = null;
        }
    }
}
