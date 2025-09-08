using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Threading;
using Cysharp.Threading.Tasks;
using VContainer;
using UniRx;
using System;

public class GameManager : MonoBehaviour
{
    public GameObject[] n;        // 2,4,8,... 프리팹 17개
    public GameObject Quit;       // 게임오버 패널
    public Text Score, BestScore, Plus;

    [Header("Layout")]
    [SerializeField] Vector2 cellSize = new(1.2f, 1.2f);
    [SerializeField] Vector2 originOffset = new(-1.8f, -1.8f);

    [Header("Input")]
    [SerializeField] float swipeThresholdPixels = 100f;
    [SerializeField] bool useDpiForSwipe = true;
    [SerializeField, Range(0.05f, 0.5f)] float swipeThresholdInches = 0.20f;

    enum Dir { Up, Down, Left, Right, None }
    Dir GetSwipeDir(Vector3 nrm)
    {
        if (Mathf.Abs(nrm.x) > Mathf.Abs(nrm.y))
            return nrm.x > 0 ? Dir.Right : Dir.Left;
        else
            return nrm.y > 0 ? Dir.Up : Dir.Down;
    }

    TileManager tm;

    // ★ UniRx 이벤트 허브
    private GameEvents events;
    private IDisposable inputSub;

    [Inject]
    public void Construct(TileManager tileManager, GameEvents events) // ★ GameEvents 주입 추가
    {
        this.tm = tileManager;
        this.events = events;      // ★ 보관
        Debug.Log($"[DI] GameManager->TileManager id={tileManager.GetInstanceID()}");
    }

    bool swiping, swipeConsumed, inputIsTouch;
    Vector3 firstPos;
    bool movedThisTurn, stopped;
    int addScore;

    // UniTask 턴 실행 상태
    bool turnRunning;
    CancellationTokenSource turnCts;

	private void OnEnable()
	{
        inputSub = events?.Input
            .ThrottleFirst(TimeSpan.FromMilliseconds(50))
            .Where(_ => !turnRunning && !TurnAnimTracker.Busy && !stopped)
            .Subscribe(dir => {
                turnCts?.Cancel();
                turnCts?.Dispose();
                turnCts = new CancellationTokenSource();
                RunTurnAsync(dir, turnCts.Token).Forget();
            });
    }

	private void OnDisable()
	{
        inputSub?.Dispose();
    }

	void Start()
    {
        if (BestScore) BestScore.text = PlayerPrefs.GetInt("BestScore").ToString();
        if (Score && string.IsNullOrEmpty(Score.text)) Score.text = "0";

        if (!tm)
            return;

        // 주입된 tm에만 초기화 호출
        tm.Setup(n, cellSize, originOffset, 4, 4);
        tm.Spawn(CurrentScore());
        tm.Spawn(CurrentScore());
    }

    void OnDestroy()
    {
        turnCts?.Cancel();
        turnCts?.Dispose();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) Application.Quit();
        if (stopped) return;

        // 애니 중이거나 턴 로직이 돌고 있으면 입력 차단
        if (turnRunning || TurnAnimTracker.Busy) return;

        // (선택) 키보드 방향키도 같은 스트림으로 통합
        if (Input.GetKeyDown(KeyCode.UpArrow)) events?.Input.OnNext(TileManager.Dir.Up);
        if (Input.GetKeyDown(KeyCode.DownArrow)) events?.Input.OnNext(TileManager.Dir.Down);
        if (Input.GetKeyDown(KeyCode.LeftArrow)) events?.Input.OnNext(TileManager.Dir.Left);
        if (Input.GetKeyDown(KeyCode.RightArrow)) events?.Input.OnNext(TileManager.Dir.Right);

        if (BeginPressed())
        {
            swiping = true;
            swipeConsumed = false;
            inputIsTouch = (Input.touchCount > 0);
            firstPos = CurrentPointerPos();
        }

        if (swiping && Holding())
        {
            var gap = CurrentPointerPos() - firstPos;
            float threshold = useDpiForSwipe && Screen.dpi > 0f
                ? swipeThresholdInches * Screen.dpi
                : swipeThresholdPixels;

            if (!swipeConsumed && gap.magnitude >= threshold)
            {
                swiping = false;       // 한 번만 쏘고 스와이프 종료 (선호에 따라 유지/삭제)
                swipeConsumed = true;

                var dir = GetSwipeDir(gap.normalized);
                if (dir == Dir.None) return;

                var tdir = dir switch
                {
                    Dir.Up => TileManager.Dir.Up,
                    Dir.Down => TileManager.Dir.Down,
                    Dir.Left => TileManager.Dir.Left,
                    Dir.Right => TileManager.Dir.Right,
                    _ => TileManager.Dir.Left
                };

                // ★ 변경 포인트: 실행 대신 발행만!
                events?.Input.OnNext(tdir);
            }
        }

        if (Released())
        {
            swiping = false;
            swipeConsumed = false;
        }
    }


    async UniTaskVoid RunTurnAsync(TileManager.Dir tdir, CancellationToken ct)
    {
        if (turnRunning) return;
        turnRunning = true;

        // ★ 턴 시작 이벤트
        events?.TurnStarted.OnNext(Unit.Default);

        movedThisTurn = false;
        addScore = 0;

        // 1) 값 로직 처리(슬라이드/머지). 여기서 이동 트윈들이 시작됨
        bool moved = tm.Sweep(tdir, out int gained, out bool anyMove);
        movedThisTurn = anyMove;
        addScore += gained;

        if (movedThisTurn)
        {
            // 2) 모든 슬라이드/머지 트윈이 끝날 때까지 대기
            await TurnAwaiter.WaitAnimationsIdleAsync(ct);
            if (ct.IsCancellationRequested) { turnRunning = false; return; }

            // 3) 스폰 + 점수 적용 (스폰팝이 시작됨)
            tm.Spawn(CurrentScore());
            ApplyScore();

            // 4) 스폰팝/머지 임팩트 등 잔여 트윈 대기
            await TurnAwaiter.WaitAnimationsIdleAsync(ct);
            if (ct.IsCancellationRequested) { turnRunning = false; return; }

            // 5) 게임오버 체크
            if (tm.IsGameOver()) { stopped = true; if (Quit) Quit.SetActive(true); }
        }

        // ★ 턴 종료 이벤트(이동 여부 포함)
        events?.TurnEnded.OnNext(movedThisTurn);

        turnRunning = false;
    }

    void ApplyScore()
    {
        if (addScore <= 0) return;

        if (Plus)
        {
            Plus.text = $"+{addScore}    ";
            var anim = Plus.GetComponent<Animator>();
            if (anim) { anim.SetTrigger("PlusBack"); anim.SetTrigger("Plus"); }
        }

        int s = CurrentScore() + addScore;

        // UI 반영
        if (Score) Score.text = s.ToString();

        // 베스트 갱신 체크
        bool bestUpdated = false;
        if (BestScore)
        {
            if (PlayerPrefs.GetInt("BestScore", 0) < s)
            {
                PlayerPrefs.SetInt("BestScore", s);
                bestUpdated = true;
            }
            BestScore.text = PlayerPrefs.GetInt("BestScore").ToString();
        }

        // ★ 점수/베스트 이벤트 발행
        events?.ScoreChanged.OnNext(new ScoreChangedEvent(s, addScore));
        if (bestUpdated)
        {
            events?.BestChanged.OnNext(PlayerPrefs.GetInt("BestScore"));
        }

        addScore = 0;
    }

    int CurrentScore() => Score ? int.Parse(Score.text) : 0;

    Vector3 CurrentPointerPos()
    {
        if (inputIsTouch && Input.touchCount > 0) return Input.GetTouch(0).position;
        return Input.mousePosition;
    }

    bool BeginPressed()
    {
        if (Input.touchCount == 1 && Input.GetTouch(0).phase == TouchPhase.Began) return true;
        return Input.GetMouseButtonDown(0);
    }

    bool Holding()
    {
        if (inputIsTouch)
        {
            if (Input.touchCount == 0) return false;
            var ph = Input.GetTouch(0).phase;
            return ph == TouchPhase.Moved || ph == TouchPhase.Stationary;
        }
        else return Input.GetMouseButton(0);
    }

    bool Released()
    {
        if (inputIsTouch)
        {
            if (Input.touchCount == 0) return true;
            var ph = Input.GetTouch(0).phase;
            return ph == TouchPhase.Ended || TouchPhase.Canceled == ph;
        }
        else return Input.GetMouseButtonUp(0);
    }

    public void Restart() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
}
