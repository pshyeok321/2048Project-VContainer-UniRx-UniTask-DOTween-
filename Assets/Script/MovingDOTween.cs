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
	[SerializeField] bool useUnscaledTime = false;

	Tween activeTween;

	public void SetLayout(Vector2 cellSize, Vector2 originOffset)
	{
		this.cellSize = cellSize;
		this.originOffset = originOffset;
	}

	public void Move(int x2, int y2, bool combine)
	{
		Vector3 targetPos = new(
			originOffset.x + cellSize.x * x2,
			originOffset.y + cellSize.y * y2,
			0f
		);

		Vector3 startPos = transform.position;
		int cx = Mathf.RoundToInt((startPos.x - originOffset.x) / cellSize.x);
		int cy = Mathf.RoundToInt((startPos.y - originOffset.y) / cellSize.y);
		int cells = Mathf.Abs(x2 - cx) + Mathf.Abs(y2 - cy);
		if (cells < 1)
			cells = 1;

		float duration = durationPerCell * cells;

		KillActive(false);

		bool completed = false;
		TurnAnimTracker.Inc(); // ✅ 트윈 참여

		activeTween = transform.DOMove(targetPos, duration)
			.SetEase(ease)
			.SetUpdate(useUnscaledTime)
			.OnComplete(() =>
			{
				completed = true;
				activeTween = null;
				if (combine)
					Destroy(gameObject);
				TurnAnimTracker.Dec(); // ✅ 정상 완료
			})
			.OnKill(() =>
			{
				// AutoKill(완료 후)에도 OnKill이 호출되므로 이중 Dec 방지
				if (!completed)
					TurnAnimTracker.Dec(); // ✅ 강제 종료
				activeTween = null;
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
