# 2048 — VContainer · UniRx · UniTask · MV(R)P · DOTween (v2.1)

작성자: **박성혁**  
문의: 이슈로 남겨주시면 친절히 답변드려요!

---

## 목차
- [개요](#개요)
  - [프로젝트 소개](#프로젝트-소개)
  - [핵심 목표](#핵심-목표)
  - [함께 사용된 라이브러리](#함께-사용된-라이브러리)
- [아키텍처](#아키텍처)
  - [구조](#구조)
  - [이벤트 스트림(UniRx)](#이벤트-스트림unirx)
  - [턴 오케스트레이션(UniTask)](#턴-오케스트레이션unitask)
  - [DI 바인딩(VContainer)](#di-바인딩vcontainer)
  - [오브젝트 풀링](#오브젝트-풀링)
- [왜 **순수 MVP** 대신 **MV(R)P 스타일**인가?](#왜-순수-mvp-대신-mvrp-스타일인가)
  - [장점](#장점)
  - [트레이드오프(인정하는-부분)](#트레이드오프인정하는-부분)
  - [순수 MVP로의 단계적 전환 경로](#순수-mvp로의-단계적-전환-경로)
- [LinkedPool 채택 이유](#linkedpool-채택-이유)
- [빠른 시작](#빠른-시작)
  - [프로젝트 열기 & 의존성](#프로젝트-열기--의존성)
  - [씬 세팅 체크리스트](#씬-세팅-체크리스트)
- [구현된 기능](#구현된-기능)
- [변경 이력(요약)](#변경-이력요약)

---

## 개요

### 프로젝트 소개
**2048** 핵심 규칙을 유지하면서 **VContainer + UniRx + UniTask + DOTween**을 활용해 **MV(R)P 스타일**로 재구성한 Unity 프로젝트입니다.  
목표는 **로직/뷰/인프라 분리**, **테스트·확장성 향상**, **스트림 기반의 깔끔한 흐름**입니다.

### 핵심 목표
- 입력/점수/스폰/머지/턴 상태를 **이벤트 스트림(UniRx)** 으로 브로드캐스트
- 턴 진행을 **비동기(UniTask)** 로 직관적·안전하게 오케스트레이션
- **VContainer**로 의존성과 설정의 **단일 출처** 유지
- **DOTween**으로 이동/스폰팝/머지 임팩트 연출
- **MV(R)P 정신**으로 Model-View-Presenter 경계 명확화 (완전 순수 MVP는 아님)

### 함께 사용된 라이브러리
- **VContainer** — 의존성 주입/수명 관리
- **UniRx** — 입력·상태 변화 이벤트 스트림
- **UniTask** — 비동기 턴 루틴(`await`/취소)
- **DOTween** — 이동/임팩트/스폰 팝 애니메이션
- **Unity LinkedPool** — 타일 오브젝트 풀링

---

## 아키텍처

### 구조
- **Model — `TileManager`**  
  보드(Grid) 상태, 스폰, 슬라이드/머지 규칙. *(필요한 레이아웃 값은 초기화 시 주입받음)*
- **Presenter(Orchestrator) — `GameManager`**  
  입력 스트림 구독 → `RunTurnAsync`(UniTask)로 턴 진행 → 이벤트 발행.
- **UI Presenters (View 갱신 담당)** — `ScorePresenter`, `PlusPresenter`, `GameOverPresenter`  
  `GameEvents`를 **구독**해 **실제 View(Text/Animator/Panel)** 를 갱신.
- **Reactive Bus — `GameEvents`(Singleton)**  
  입력/턴/점수/스폰/머지/게임오버 등 중앙 이벤트 허브.
- **Infra — `PooledTileFactory`(풀링), `SystemRandomProvider`(RNG)**

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

// 팩토리: 프리팹 + 레이아웃 값 직접 주입
builder.Register<ITileFactory, PooledTileFactory>(Lifetime.Singleton)
       .WithParameter("prefabs", tilePrefabs)
       .WithParameter("cellSize", cellSize)
       .WithParameter("originOffset", originOffset);

// ★ 보드 초기화는 컨테이너 빌드 직후 1회 수행
builder.RegisterBuildCallback(c =>
{
    var tm = c.Resolve<TileManager>();
    tm.Setup(null, cellSize, originOffset, width, height); // prefab은 팩토리가 관리
});
```

### 오브젝트 풀링
- `PooledTileFactory`가 **타일 생성/회수/위치 지정/레이아웃 전달**까지 담당
- 프리팹 인덱스 규칙: `idx = log2(value) - 1` (2→0, 4→1, 8→2 …) + **클램프**
- 풀 태그(`PooledTileTag.poolIndex`)로 안전 반납

---

## 왜 **순수 MVP** 대신 **MV(R)P 스타일**인가?

이 프로젝트는 **실용성·속도·안정성**을 우선해, Unity 친화적 워크플로에 맞춘 **MV(R)P 스타일**을 선택했습니다.  
일부 View 의존(애니/FX 등)이 Model 내부에 남아 있어 **엄밀한 MVP**는 아니지만, 아래 장점 때문에 이 경로를 택했습니다.

### 장점
1. **변경 범위 최소화 → 리스크↓ / 속도↑**  
   대규모 리팩터링 없이 도메인 규칙을 보존. Rx 이벤트 “탭-아웃”만 추가해도 UI/사운드/카메라를 **구독자**로 쉽게 연결.
2. **Unity 친화성 & 협업 효율**  
   프레젠터를 **씬의 MonoBehaviour**로 두면 인스펙터 바인딩, DOTween·Animator 연결이 즉시 가능.  
   디자이너/TA와의 협업에 유리.
3. **디버깅 가시성**  
   `GameEvents` 중심으로 로그/계측을 한곳에서 관찰. 씬에서 프레젠터 활성/바인딩 누락 여부를 눈으로 확인.
4. **성능 & 예측 가능성**  
   `LinkedPool`로 프레임 내 다량의 Spawn/Release에도 **O(1)** 로 안정.  
   Model–View 사이에 별도 ID/레지스트리 계층을 강제하지 않아 초기 오버헤드가 적음.
5. **점진적 이행이 쉬움**  
   경계를 **스트림으로 노출**해 두었기 때문에, 필요 시 언제든 순수 MVP로 진화 가능.

### 트레이드오프(인정하는 부분)
- `TileManager`에 남아있는 **연출 호출(DOTween/FX)** 은 순수 MVP 원칙과 다소 어긋남.  
- `GameManager`가 **입력 감지 일부**를 아직 다룸(실행은 Rx 구독부에서 제어).  
- **PlayerPrefs 접근**이 추상화(예: `IScoreStore`)되기 전이라 테스트 대역 교체가 제한적.

### 순수 MVP로의 단계적 전환 경로
1. **입력 감지 → View로 분리**: `InputAdapter`(Mono)가 감지 → `events.Input.OnNext(dir)`만 발행.  
2. **영속 분리**: `IScoreStore`(예: `PlayerPrefsScoreStore`) 도입, `GameManager`는 인터페이스만 사용.  
3. **모델 이벤트 확장**: `TileMoved`, `TileMerged`, `TileSpawned` 더 풍부하게 발행.  
4. **TileViewPresenter** 신설: 애니/FX/뷰 생성/회수 전담 → 이후 `TileManager`의 뷰 호출 제거.  
5. **Grid 순수화**: `GameObject` 제거, id/value/flags만 보관 → **완전한 Model 테스트** 가능.

---

## LinkedPool 채택 이유

**왜 커스텀 풀 대신?**  
- 표준 API가 **중복 Release/미등록 Release** 등 흔한 버그를 `collectionCheck`로 차단.  
- `actionOnGet/Release/Destroy` 훅으로 활성/비활성/정리를 통일. 유지보수 비용↓.

**왜 `ObjectPool<T>` 대신 `LinkedPool<T>`?**
- `LinkedPool<T>`는 연결 리스트 기반으로 **리사이즈 없음** → **추가 할당/복사 0, GC 스파이크 방지**.  
- **O(1)** Push/Pop으로 프레임 내 **대량 Spawn/Release**가 잦은 2048에 유리.  
- 설정이 단순(`maxSize`만 신경)해 **보드 크기(N×N) 변경**에도 용량 튜닝 부담이 적음.

> `ObjectPool<T>`는 풀 크기가 **고정**이고 초기 용량을 정확히 산정할 수 있을 때 효과적입니다.  
> 반면 2048처럼 **한 턴에 다수의 Release → Spawn**이 빈번하고, **보드 크기가 가변**인 경우 `LinkedPool<T)`가 더 예측 가능하고 튜닝이 쉽습니다.

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
- **3rdParty 추가** — VContainer, UniRx, UniTask, DOTween Package
- **DPI 보정 스와이프 임계, 턴 후 입력 잠금**
  - 기기 해상도 보정, 애니 중 과입력 차단
- **MovingDOTween 추가** — DOTween 이동 로직
- **TileFXDOTween, TurnAnimTracker 추가**
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
- **레이아웃 단일 출처(씬 인스펙터) 확립**  
  - `GameLifetimeScope` 인스펙터에서 `cellSize/originOffset/width/height` 지정  
  - BuildCallback에서 `TileManager.Setup(null, cellSize, originOffset, width, height)` 1회 호출

---