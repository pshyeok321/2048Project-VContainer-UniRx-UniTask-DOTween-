using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Threading;
using Cysharp.Threading.Tasks;

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

    bool swiping, swipeConsumed, inputIsTouch;
    Vector3 firstPos;
    bool movedThisTurn, stopped;
    int addScore;

    // UniTask 턴 실행 상태
    bool turnRunning;
    CancellationTokenSource turnCts;

    void Start()
    {
        if (BestScore) BestScore.text = PlayerPrefs.GetInt("BestScore").ToString();
        if (Score && string.IsNullOrEmpty(Score.text)) Score.text = "0";

        tm = gameObject.AddComponent<TileManager>();
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

                // 🔄 이전 턴 취소(안전) 후, 새 턴 실행
                turnCts?.Cancel();
                turnCts?.Dispose();
                turnCts = new CancellationTokenSource();

                RunTurnAsync(tdir, turnCts.Token).Forget();
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
        if (Score) Score.text = s.ToString();

        if (BestScore)
        {
            if (PlayerPrefs.GetInt("BestScore", 0) < s) PlayerPrefs.SetInt("BestScore", s);
            BestScore.text = PlayerPrefs.GetInt("BestScore").ToString();
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
