using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public GameObject[] n;        // 2,4,8,... 프리팹 17개
    public GameObject Quit;       // 게임오버 패널
    public Text Score, BestScore, Plus;

    [Header("Layout")]
    [SerializeField] Vector2 cellSize = new(1.2f, 1.2f);
    [SerializeField] Vector2 originOffset = new(-1.8f, -1.8f);

    [Header("Input")]
    [SerializeField] float swipeThresholdPixels = 100f;     // 기본값 (DPI 비활성 시 사용)
    [SerializeField] bool useDpiForSwipe = true;            // ✅ DPI 보정 사용
    [SerializeField] [Range(0.05f, 0.5f)] float swipeThresholdInches = 0.20f; // 0.2 inch 권장
    [SerializeField] float postMoveInputLockSeconds = 0.12f; // ✅ 이동 후 입력 잠금 (중복 스와이프 방지)

    enum Dir { Up, Down, Left, Right, None }
    Dir GetSwipeDir(Vector3 nrm)
    {
        // ✅ 드래그한 방향 = 타일 이동 방향
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

    // 입력 잠금(간단 쿨다운)
    float inputUnlockAtUnscaled = 0f;
    bool InputLocked => Time.unscaledTime < inputUnlockAtUnscaled;

    void Start()
    {
        if (BestScore) BestScore.text = PlayerPrefs.GetInt("BestScore").ToString();
        if (Score && string.IsNullOrEmpty(Score.text)) Score.text = "0";

        tm = gameObject.AddComponent<TileManager>();
        tm.Setup(n, cellSize, originOffset, 4, 4);

        tm.Spawn(CurrentScore());
        tm.Spawn(CurrentScore());
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) Application.Quit();
        if (stopped) return;

        if (InputLocked) // ✅ 이동 직후 잠깐 입력 무시
            return;

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

            // ✅ DPI 보정된 스와이프 임계값 계산
            float threshold = useDpiForSwipe && Screen.dpi > 0f
                ? swipeThresholdInches * Screen.dpi
                : swipeThresholdPixels;

            if (!swipeConsumed && gap.magnitude >= threshold)
            {
                swipeConsumed = true;
                var dir = GetSwipeDir(gap.normalized);
                if (dir == Dir.None) return;

                movedThisTurn = false;
                addScore = 0;

                var tdir = dir switch
                {
                    Dir.Up => TileManager.Dir.Up,
                    Dir.Down => TileManager.Dir.Down,
                    Dir.Left => TileManager.Dir.Left,
                    Dir.Right => TileManager.Dir.Right,
                    _ => TileManager.Dir.Left
                };

                bool moved = tm.Sweep(tdir, out int gained, out bool anyMove);
                movedThisTurn = anyMove;
                addScore += gained;

                if (movedThisTurn)
                {
                    // ✅ 이동 성공 시, 잠깐 입력 잠금으로 중복 스와이프 방지
                    inputUnlockAtUnscaled = Time.unscaledTime + Mathf.Max(0.08f, postMoveInputLockSeconds);

                    tm.Spawn(CurrentScore());
                    ApplyScore();
                    if (tm.IsGameOver()) { stopped = true; if (Quit) Quit.SetActive(true); }
                }
            }
        }

        if (Released())
        {
            swiping = false;
            swipeConsumed = false;
        }
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
            return ph == TouchPhase.Ended || ph == TouchPhase.Canceled;
        }
        else return Input.GetMouseButtonUp(0);
    }

    public void Restart() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
}
