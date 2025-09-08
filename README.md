# 2048 — VContainer · UniRx · UniTask · MV(R)P · DOTween

작성자: **박성혁**
문의: 이슈로 남겨주시면 친절히 답변드려요!

---

## 목차
- [개요](#개요)
  - [프로젝트 소개](#프로젝트-소개)
  - [핵심 목표](#핵심-목표)
  - [함께 사용된 라이브러리](#함께-사용된-라이브러리)
- [아키텍처](#아키텍처)
  - [전체 그림](#전체-그림)
  - [이벤트 스트림(UniRx)](#이벤트-스트림unirx)
  - [턴 오케스트레이션(UniTask)](#턴-오케스트레이션unitask)
  - [DI 바인딩(VContainer)](#di-바인딩vcontainer)
  - [오브젝트 풀링](#오브젝트-풀링)
- [빠른 시작](#빠른-시작)
  - [프로젝트 열기 & 의존성](#프로젝트-열기--의존성)
  - [씬 세팅 체크리스트](#씬-세팅-체크리스트)
- [폴더 구조](#폴더-구조)
- [구현된 기능](#구현된-기능)
- [변경 이력(요약)](#변경-이력요약)
- [트러블슈팅](#트러블슈팅)
- [확장 가이드](#확장-가이드)
- [라이선스](#라이선스)

---

## 개요

### 프로젝트 소개
**2048** 핵심 규칙을 유지하면서 **VContainer + UniRx + UniTask + DOTween**을 활용해 **MV(R)P** 구조로 재구성한 Unity 프로젝트입니다.
목표는 **로직/뷰/인프라 분리**, **테스트·확장성 향상**, **스트림 기반의 깔끔한 흐름**입니다.

### 핵심 목표
- 입력/점수/스폰/머지/턴 상태를 **이벤트 스트림(UniRx)** 으로 브로드캐스트
- 턴 진행을 **비동기(UniTask)** 로 직관적·안전하게 오케스트레이션
- **VContainer**로 의존성과 설정의 **단일 출처** 유지
- **DOTween**으로 이동/스폰팝/머지 임팩트 연출
- **MV(R)P**로 Model-View-Presenter 경계 명확화

### 함께 사용된 라이브러리
- **VContainer** — 의존성 주입/수명 관리
- **UniRx** — 입력·상태 변화 이벤트 스트림
- **UniTask** — 비동기 턴 루틴(`await`/취소)
- **DOTween** — 이동/임팩트/스폰 팝 애니메이션
- **Unity LinkedPool** — 타일 오브젝트 풀링

---

## 아키텍처

### 전체 그림
- **Model** — `TileManager`
  보드(Grid) 상태, 스폰, 슬라이드/머지 규칙. (뷰 생성/좌표는 팩토리로 위임)
- **Presenter(Orchestrator)** — `GameManager`
  입력 스트림 구독 → `RunTurnAsync`(UniTask)로 턴 진행 → 이벤트 발행.
- **View (Presenters)** — `ScorePresenter`, `PlusPresenter`, `GameOverPresenter`
  `GameEvents`를 **구독**해 UI 및 연출 갱신(로직과 분리).
- **Reactive Bus** — `GameEvents`(Singleton)
  입력/턴/점수/스폰/머지/게임오버 등 중앙 이벤트 허브.
- **Infra** — `PooledTileFactory`(풀링), `BoardLayoutProvider`(레이아웃 DI), `SystemRandomProvider`(RNG)

### 이벤트 스트림(UniRx)
`GameEvents`에서 발행되는 주 스트림:
- `Subject<TileManager.Dir> Input`
- `Subject<Unit> TurnStarted`
- `Subject<bool> TurnEnded` (해당 턴에 실제 이동 여부)
- `Subject<ScoreChangedEvent>` {Score, Delta}
- `Subject<int> BestChanged`
- `Subject<TileSpawnedEvent>` {Value, Cell}
- `Subject<MergeEvent>` {Value, Count, Cell}
- `Subject<Unit> GameOver`

> UI/사운드/카메라 효과 등은 **구독자(Presenter)**만 추가하면 연동됩니다.

### 턴 오케스트레이션(UniTask)
`GameManager.RunTurnAsync(dir, ct)`:
1. `TurnStarted` 발행
2. `tm.Sweep(dir, out gained, out anyMove)` → 슬라이드/머지 트윈 시작
3. `await TurnAwaiter.WaitAnimationsIdleAsync(ct)` → 슬라이드/머지 완료 대기
4. 스폰(`tm.Spawn`), 점수 반영 → `ScoreChanged`/`BestChanged`
5. `await TurnAwaiter.WaitAnimationsIdleAsync(ct)` → 스폰팝/임팩트 대기
6. 게임오버 시 `GameOver` 발행, 아니면 `TurnEnded` 후 다음 입력 대기

### DI 바인딩(VContainer)
```csharp
// GameLifetimeScope.Configure(...)
builder.RegisterComponentInHierarchy<GameManager>();
builder.RegisterComponentInHierarchy<TileManager>();

builder.RegisterComponentInHierarchy<ScorePresenter>();
builder.RegisterComponentInHierarchy<PlusPresenter>();
builder.RegisterComponentInHierarchy<GameOverPresenter>();

builder.Register<GameEvents>(Lifetime.Singleton);
builder.Register<IRandomProvider, SystemRandomProvider>(Lifetime.Singleton);

builder.Register<IBoardLayout, BoardLayoutProvider>(Lifetime.Singleton)
       .WithParameter("cellSize", cellSize)
       .WithParameter("originOffset", originOffset)
       .WithParameter("width", width)
       .WithParameter("height", height);

builder.Register<ITileFactory, PooledTileFactory>(Lifetime.Singleton)
       .WithParameter("prefabs", tilePrefabs);

builder.RegisterBuildCallback(c => c.Resolve<TileManager>().Initialize());
```

### 오브젝트 풀링
- `PooledTileFactory`가 **타일 생성/회수/위치 지정/레이아웃 전달**까지 담당
- 프리팹 인덱스 규칙: `idx = log2(value) - 1` (2→0, 4→1, 8→2 …) + **클램프**
- 풀 태그(`PooledTileTag.poolIndex`)로 안전 반납

---

## 빠른 시작

### 프로젝트 열기 & 의존성
1. Unity에서 프로젝트를 엽니다.
2. 패키지 설치
   - VContainer, UniRx, UniTask, DOTween
3. DOTween Setup 실행(필요 시 `Tools/Demigiant/DOTween Utility Panel`).

### 씬 세팅 체크리스트
- **GameLifetimeScope** 1개(Enabled)
  - `tilePrefabs`: (2,4,8,16,…) 순서로 배열 할당
  - `cellSize`, `originOffset`, `width`, `height` 지정
- **씬 컴포넌트**: `GameManager`, `TileManager`, `ScorePresenter`, `PlusPresenter`, `GameOverPresenter` 존재/Enabled
- **플레이 시 확인**
  - 시작과 동시에 `TileSpawned` 2회
  - 스와이프 후 `TurnStarted → (Merge/ScoreChanged/TileSpawned) → TurnEnded`

---

## 폴더 구조
```
Assets/
  Scripts/
    GameLifetimeScope.cs
    GameManager.cs
    TileManager.cs
    Interfaces.cs                 // IRandomProvider, ITileFactory
    SystemRandomProvider.cs
    PooledTileFactory.cs (+ PooledTileTag.cs)
    GameEvents.cs

    Presenters/
      ScorePresenter.cs
      PlusPresenter.cs
      GameOverPresenter.cs
      DebugEventLogger.cs (옵션)

    View/
      MovingDOTween.cs
      TileFXDOTween.cs
      TurnAnimTracker.cs

    Layout/
      IBoardLayout.cs
      BoardLayoutProvider.cs
```

---

## 구현된 기능
- **스와이프 입력**: DPI 기준 임계값, 드래그-투-스와이프, 중복 입력 방지
- **슬라이드/머지 규칙**: 2048 표준 규칙
- **DOTween 연출**: 이동/스폰 팝/머지 임팩트
- **오브젝트 풀링**: LinkedPool 기반 팩토리
- **UniTask**: 턴 진행을 await 체인으로 명확화
- **UniRx**: 입력/턴/점수/스폰/머지/게임오버 스트림화
- **MV(R)P 분리**: 로직은 Model, UI는 Presenter(구독자)가 담당

---

## 변경 이력(요약)
> 실제 커밋 메시지를 바탕으로 간결히 정리.

- **TileManager 추가, 기존 로직 시스템 변경**
  - `GameManager`: 입력/스코어/게임 상태
  - `TileManager`: 격자 상태, 스폰, 슬라이드/머지, *(초기에는 좌표 변환 포함)*
- **3rdParty 추가** — VContainer, UniRx, UniTask, DOTween Package (`6b82a5`)
- **DPI 보정 스와이프 임계, 턴 후 입력 잠금** (`4ae72f`)
  - 기기 해상도 보정, 애니 중 과입력 차단
- **MovingDOTween 추가** — DOTween 이동 로직 (`1694dc`)
- **TileFXDOTween, TurnAnimTracker 추가** (`75bc03`)
  - 스폰 팝/머지 임팩트, 트윈 Busy 추적
- **UniTask로 턴 오케스트레이션 기능 추가**
  - 스와이프 → (슬라이드/머지 대기) → 스폰팝 → (대기) → 게임오버 체크
- **DI 인터페이스 & 풀링 도입**
  - `IRandomProvider`, `ITileFactory`, `PooledTileFactory`(+ `PooledTileTag`)
  - `TileManager` 스폰/머지에서 팩토리 사용, 릴리스 일원화
  - `GameManager`의 AddComponent 제거 → DI 고정
- **UniRx 이벤트 스트림화 + Presenter 분리**
  - `GameEvents` (입력/턴/점수/스폰/머지/게임오버)
  - `ScorePresenter`, `PlusPresenter`, `GameOverPresenter`
  - `DebugEventLogger`(옵션)
- **레이아웃 단일 출처 확립**
  - `IBoardLayout/BoardLayoutProvider` 도입 → `TileManager.Initialize()`
  - `PooledTileFactory`가 위치/레이아웃 세팅까지 담당 → *Model의 뷰 의존 제거*

---

## 트러블슈팅
- **플레이 시 초기 스폰이 안 됨**
  - DI 누락으로 `tm == null` → `RegisterComponentInHierarchy<GameManager/TileManager>` 확인
- **`IndexOutOfRangeException` in `pools[idx].Get()`**
  - `tilePrefabs` 미주입/개수 부족/순서 오류(2,4,8,…) → 인스펙터 확인
- **이벤트 로그가 안 찍힘(Null)**
  - Presenter/Logger를 `RegisterComponentInHierarchy<...>()`로 등록했는지 확인
- **중복 입력/연타 문제**
  - 입력 구독부에 `.ThrottleFirst(TimeSpan.FromMilliseconds(50))` 적용

---

## 확장 가이드
- **UI/사운드/카메라 효과 추가**: Presenter를 만들고 `GameEvents`를 **구독**하세요.
- **오토플레이/리플레이**: `events.Input.OnNext(dir)`에 시나리오 값 공급.
- **풀 워밍업**: 팩토리에 `Warmup(countPerPrefab)` 추가 후 BuildCallback에서 호출.

---

## 라이선스
- 예제/학습 목적의 프로젝트입니다. 별도 명시가 없는 리소스는 해당 저작권을 따릅니다.
