# 2048 By VContainer-UniRx UniTask-MV(R)P-DOTween

---

작성자 : 박성혁

궁금한 점 있을 시 Issue 남겨주시면 친절히 답변해드립니다!



목표: 2048 게임을 모듈화하고, 입력/점수/타일 이벤트를 스트림화(UniRx), 턴 진행은 비동기(UniTask), 의존성은 VContainer로 주입.

아키텍처: MV(R)P — TileManager(Model), GameManager(Presenter/오케스트레이션), View(Presenters), GameEvents(Reactive Bus).

효과: 로직/뷰/연출 분리, 테스트/확장 용이, 설정의 단일 출처(씬의 LifetimeScope).

기술 스택

VContainer: DI/라이프사이클 관리 (씬 컴포넌트 vs 순수 서비스 분리)

UniRx: 입력/턴/점수/스폰/머지/게임오버 이벤트 스트림

UniTask: 턴 오케스트레이션(애니 대기, 취소, 순차 플로우)

DOTween: 이동/스폰팝/머지 임팩트

LinkedPool: 타일 오브젝트 풀링 (PooledTileFactory)

현재 구조 (MV(R)P)
Model

TileManager

보드 상태(Grid), 스폰, 슬라이드/머지 규칙

팩토리 기반 생성/회수(뷰 생성은 팩토리가 담당)

이벤트 발행: Merge, TileSpawned

Presenter (오케스트레이션)

GameManager

입력 스트림 구독 → RunTurnAsync 실행(UniTask)

턴 이벤트 발행: TurnStarted, TurnEnded

점수 상태 관리 및 이벤트 발행: ScoreChanged, BestChanged

게임오버 시 GameOver 발행

View (구독자)

ScorePresenter: ScoreChanged, BestChanged 구독 → 점수/베스트 UI 갱신

PlusPresenter: ScoreChanged(Delta>0) 구독 → +스코어 텍스트 & 애니

GameOverPresenter: GameOver 구독 → Quit 패널 표시


Reactive Bus

GameEvents (Singleton)

Subject<TileManager.Dir> Input

Subject<Unit> TurnStarted

Subject<bool> TurnEnded (moved)

Subject<ScoreChangedEvent> {Score, Delta}

Subject<int> BestChanged

Subject<TileSpawnedEvent> {Value, Cell}

Subject<MergeEvent> {Value, Count, Cell}

Subject<Unit> GameOver

턴 진행(오케스트레이션, UniTask)

입력 발생 → events.Input.OnNext(dir)

OnEnable에서 입력 스트림 구독(ThrottleFirst(50ms)) → RunTurnAsync(dir, ct)

tm.Sweep → 슬라이드/머지 시작 → await TurnAwaiter.WaitAnimationsIdleAsync()

스폰 → 점수 반영 → 다시 await (스폰팝/임팩트 종료)

게임오버 체크 → GameOver 발행 or 다음 입력 대기

입력(스트림화, UniRx)

Update는 방향만 계산 → events.Input.OnNext(tdir)

중복 입력 방지: 구독부에서 .ThrottleFirst(TimeSpan.FromMilliseconds(50)) + !turnRunning && !TurnAnimTracker.Busy

오브젝트 풀링

PooledTileFactory

LinkedPool<GameObject>[] pools (프리팹별 풀)

ValueToIndex = log2(value) - 1 (2→0, 4→1, 8→2, …), 클램프로 안전

Create(cell, value, parent)에서 월드 좌표/레이아웃 세팅까지 담당

Release(go)는 풀로 반환

씬의 tilePrefabs는 (2,4,8,16,...) 순서로 할당

DI & 설정(단일 출처: GameLifetimeScope)

씬 컴포넌트 주입

RegisterComponentInHierarchy<GameManager>()

RegisterComponentInHierarchy<TileManager>()

RegisterComponentInHierarchy<ScorePresenter>()

RegisterComponentInHierarchy<PlusPresenter>()

RegisterComponentInHierarchy<GameOverPresenter>()

서비스

Register<GameEvents>(Lifetime.Singleton)

Register<IRandomProvider, SystemRandomProvider>(Lifetime.Singleton)

Register<IBoardLayout, BoardLayoutProvider>(Lifetime.Singleton)
cellSize, originOffset, width, height 값을 인스펙터에서 설정

Register<ITileFactory, PooledTileFactory>(Lifetime.Singleton)
.WithParameter("prefabs", tilePrefabs) (인스펙터에서 (2,4,8,...) 순서로 지정)

초기화

RegisterBuildCallback(c => c.Resolve<TileManager>().Initialize());
→ 보드 그리드 크기 생성(레이아웃에서 width/height 읽음)

**GameManager.Start()**는 초기 스폰만 수행 (레이아웃/프리팹 모름)

장점

MV(R)P 준수: 로직/뷰 완전 분리, 테스트 용이

스트림화: UI/사운드/연출은 “구독만”으로 연결

안정적 턴 흐름: UniTask로 가독성과 동기화 확보

설정 단일화: 레이아웃/프리팹은 LifetimeScope 1곳에서 관리

씬 체크리스트

 GameLifetimeScope 1개 (Enabled)

tilePrefabs에 (2,4,8,…) 프리팹 순서대로

cellSize, originOffset, width, height 설정

 GameManager, TileManager, ScorePresenter, PlusPresenter, GameOverPresenter 씬에 존재 & Enabled

 LifetimeScope에 위 등록 코드 반영

 플레이 시작 시 TileSpawned 2회, 스와이프 시 TurnStarted→…→TurnEnded 로그 확인

변경 이력(커밋 로그 요약)
1) 초기 구조 정리

TileManager 추가, 기존 로직 시스템 변경

GameManager: 입력/스코어/게임 상태

TileManager: 격자 상태, 스폰, 슬라이드/머지, 좌표 변환

2) 3rdParty 추가 — VContainer/UniRx/UniTask/DOTween 6b82a5

DI 도입, 이벤트/비동기/연출 기반 마련

3) 입력 UX 개선 — DPI 보정 & 턴 후 입력 잠금 4ae72f

DPI 기반 스와이프 임계값

턴/애니 중 입력 차단

4) 이동 연출 — MovingDOTween 1694dc

DOTween으로 타일 이동 트윈

5) FX/트래커 — TileFXDOTween, TurnAnimTracker 75bc03

스폰 팝/머지 임팩트

모든 트윈 Busy 상태 추적(ref-count)

6) UniTask로 턴 오케스트레이션

스와이프 → (슬라이드/머지 대기) → 스폰팝 → (대기) → 게임오버 체크

RunTurnAsync로 await 체인 구축, 취소 토큰으로 중복 턴 보호

7) Object Pooling & DI 인터페이스

IRandomProvider, ITileFactory

PooledTileFactory(LinkedPool) + PooledTileTag

TileManager 스폰/머지 시 팩토리 사용, 릴리스 일원화

GameManager에서 AddComponent<TileManager> 제거, DI 고정

8) UniRx 이벤트 스트림화 + 최소 Presenter

GameEvents: 중앙 이벤트 허브(입력 포함)

ScorePresenter, UniRxScorePresenter(예시), DebugEventLogger

9) MVP 정리

GameOverPresenter 분리: GameOver 이벤트 구독으로 Quit 패널 표시

Score/Plus Presenter: 점수/베스트/플러스 UI를 구독자로 이관

DI로 레이아웃/프리팹 단일 출처 확립:

IBoardLayout/BoardLayoutProvider 도입

PooledTileFactory가 뷰/좌표/레이아웃 세팅 담당

TileManager는 모델 역할에 집중(레이아웃/프리팹 비의존)