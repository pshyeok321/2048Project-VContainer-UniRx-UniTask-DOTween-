using UnityEngine;
using DG.Tweening;

public class TileFXDOTween : MonoBehaviour
{
	[Header("Spawn Pop")]
	[SerializeField] float spawnFromScale = 0.85f;
	[SerializeField] float spawnDuration = 0.12f;
	[SerializeField] Ease spawnEase = Ease.OutBack;
	[SerializeField] bool spawnUseUnscaledTime = false;

	[Header("Merge Impact")]
	[SerializeField] float mergeScaleUp = 1.12f;
	[SerializeField] float mergeScaleUpTime = 0.06f;
	[SerializeField] float mergeScaleDownTime = 0.06f;
	[SerializeField] float mergeDelayAfterSpawn = 0.02f;
	[SerializeField] Ease mergeEaseUp = Ease.OutQuad;
	[SerializeField] Ease mergeEaseDown = Ease.InQuad;
	[SerializeField] bool mergeUseUnscaledTime = false;

	Tween spawnTween;
	Tween mergeTween;
	Vector3 baseScale;
	bool baseScaleCached;

	void Awake()
	{
		CacheBaseScaleIfNeeded();
	}

	void OnDisable() { Kill(spawnTween, false); Kill(mergeTween, false); }
	void OnDestroy() { Kill(spawnTween, false); Kill(mergeTween, false); }

	void CacheBaseScaleIfNeeded()
	{
		if (!baseScaleCached)
		{
			baseScale = transform.localScale;
			baseScaleCached = true;
		}
	}

	void Kill(Tween t, bool complete)
	{
		if (t != null && t.IsActive())
			t.Kill(complete);
	}

	// ===================== Spawn =====================
	public void PlaySpawnPop() =>
		PlaySpawnPop(spawnFromScale, spawnDuration, spawnEase, spawnUseUnscaledTime);

	public void PlaySpawnPop(float fromScale, float duration, Ease ease, bool useUnscaled)
	{
		CacheBaseScaleIfNeeded();

		// 기존 스폰 트윈이 살아있다면 강제 종료(트래커 자동 Dec)
		Kill(spawnTween, false);

		Vector3 start = baseScale * Mathf.Max(0.01f, fromScale);
		transform.localScale = start;

		bool completed = false;
		TurnAnimTracker.Inc(); // ✅ 트윈 참여

		spawnTween = transform
			.DOScale(baseScale, duration)
			.SetEase(ease)
			.SetUpdate(useUnscaled)
			.OnComplete(() =>
			{
				completed = true;
				spawnTween = null;
				TurnAnimTracker.Dec(); // ✅ 정상 완료
			})
			.OnKill(() =>
			{
				if (!completed)
					TurnAnimTracker.Dec(); // ✅ 강제 종료
				spawnTween = null;
			});
	}

	public static void Spawn(GameObject go,
		float fromScale = 0.85f, float duration = 0.12f,
		Ease ease = Ease.OutBack, bool useUnscaled = false)
	{
		var fx = go.GetComponent<TileFXDOTween>() ?? go.AddComponent<TileFXDOTween>();
		fx.PlaySpawnPop(fromScale, duration, ease, useUnscaled);
	}

	// ===================== Merge =====================
	public void PlayMergeImpact() =>
		PlayMergeImpact(mergeScaleUp, mergeScaleUpTime, mergeScaleDownTime,
						mergeDelayAfterSpawn, mergeEaseUp, mergeEaseDown, mergeUseUnscaledTime);

	public void PlayMergeImpact(float scaleUp, float tUp, float tDown,
		float delay, Ease easeUp, Ease easeDown, bool useUnscaled)
	{
		CacheBaseScaleIfNeeded();

		Kill(mergeTween, false); // 중복 머지 연출 방지
		transform.localScale = baseScale;

		bool completed = false;
		TurnAnimTracker.Inc(); // ✅ 트윈 참여

		var seq = DOTween.Sequence().SetUpdate(useUnscaled);
		if (delay > 0f)
			seq.AppendInterval(delay);

		seq.Append(transform.DOScale(baseScale * Mathf.Max(1.0f, scaleUp), tUp).SetEase(easeUp));
		seq.Append(transform.DOScale(baseScale, tDown).SetEase(easeDown));

		mergeTween = seq
			.OnComplete(() =>
			{
				completed = true;
				mergeTween = null;
				TurnAnimTracker.Dec(); // ✅ 정상 완료
			})
			.OnKill(() =>
			{
				if (!completed)
					TurnAnimTracker.Dec(); // ✅ 강제 종료
				mergeTween = null;
			});
	}

	public static void Merge(GameObject go,
		float scaleUp = 1.12f, float tUp = 0.06f, float tDown = 0.06f,
		float delay = 0.02f, Ease easeUp = Ease.OutQuad, Ease easeDown = Ease.InQuad,
		bool useUnscaled = false)
	{
		var fx = go.GetComponent<TileFXDOTween>() ?? go.AddComponent<TileFXDOTween>();
		fx.PlayMergeImpact(scaleUp, tUp, tDown, delay, easeUp, easeDown, useUnscaled);
	}
}
