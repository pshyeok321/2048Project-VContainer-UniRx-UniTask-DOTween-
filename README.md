# 2048 — VContainer · UniRx · UniTask · **MVP** · DOTween (v2.2 · Full MVP)

작성자: **박성혁**  
프로젝트 버전: **Unity 2022.3.44f1**

---

## 목차
- [개요](#개요)
- [아키텍처](#아키텍처)
  - [구조](#구조)
  - [이벤트 스트림](#이벤트-스트림)
  - [턴 오케스트레이션](#턴-오케스트레이션)
  - [DI 배선](#di-배선)
  - [풀링](#풀링)
- [왜 MVP로 정리했나](#왜-mvp로-정리했나)
- [LinkedPool을 선택한 이유](#linkedpool을-선택한-이유)
- [빠른 시작](#빠른-시작)
- [구현된 기능](#구현된-기능)
- [변경 이력(요약)](#변경-이력요약)
- [프로젝트 진행 후 느낀 점](#프로젝트-진행-후-느낀-점)

---

## 개요
2048 규칙을 유지하면서 **VContainer + UniRx + UniTask + DOTween**을 사용해 **완전한 MVP**로 재구성했다.  
**Model = 순수 C#**, **View = MonoBehaviour(UI/연출 전용)**, **Presenter = Model↔View 중개(가능하면 Pure C#)** 로 분리했다.

---

## 아키텍처

### 구조
```
Scripts/
  Manager/   GameManager, TileManager
  Model/     ScoreModel, PlusModel, GameOverModel
  Presenter/ ScorePresenter, PlusPresenter, GameOverPresenter
  View/      ScoreView, PlusView, GameOverView, MovingDOTween, TileFXDOTween, TurnAnimTracker, TurnAwaiter
  Infra/     PooledTileFactory, SystemRandomProvider, Interfaces, GameEvents
```
- **Model**
  - `ScoreModel` : 현재 점수/최고 점수 스트림
  - `PlusModel`  : 가산 점수(+X) 스트림
  - `GameOverModel` : 게임오버 상태
  - (모델들은 `UnityEngine`에 의존하지 않음)
- **Presenter**
  - `ScorePresenter`, `PlusPresenter`, `GameOverPresenter` : View 인터페이스 주입 → 스트림 구독 → UI 반영
  - `GameManager` : 입력 → 턴 오케스트레이션(아래 참조)
- **View**
  - `ScoreView`, `PlusView`, `GameOverView` : 텍스트/패널/애니메이터 조작
  - 이동/임팩트는 타일 프리팹의 DOTween 컴포넌트에서 처리
- **Infra**
  - `PooledTileFactory` : 타일 생성/회수/위치 지정
  - `GameEvents` : 입력/턴/점수/스폰/머지/게임오버 등 전역 이벤트 허브

### 이벤트 스트림
- 입력/턴: `Input`, `TurnStarted`, `TurnEnded(moved)`  
- 점수: `ScoreChanged{score, delta}`, `BestChanged`  
- 타일: `TileSpawned{value, cell}`, `Merge{value, count, cell}`  
- 상태: `GameOver`

### 턴 오케스트레이션
1) `tm.Sweep(dir)` → 이동/머지 트윈 시작  
2) 트윈 종료 대기 → 스폰 + 점수 반영  
3) 스폰 팝/임팩트 종료 대기 → 게임오버 체크 → `TurnEnded`

### DI 배선
```csharp
// GameLifetimeScope.Configure(...)
builder.Register<GameEvents>(Lifetime.Singleton);
builder.Register<IRandomProvider, SystemRandomProvider>(Lifetime.Singleton);
builder.Register<ITileFactory, PooledTileFactory>(Lifetime.Singleton)
       .WithParameter("prefabs", tilePrefabs)
       .WithParameter("cellSize", cellSize)
       .WithParameter("originOffset", originOffset);

// Models
builder.Register<ScoreModel>(Lifetime.Singleton);
builder.Register<PlusModel>(Lifetime.Singleton);
builder.Register<GameOverModel>(Lifetime.Singleton);

// Views (씬에 존재하는 컴포넌트 주입)
builder.RegisterComponentInHierarchy<ScoreView>();    builder.Register<IScoreView, ScoreView>(Lifetime.Scoped);
builder.RegisterComponentInHierarchy<PlusView>();     builder.Register<IPlusView, PlusView>(Lifetime.Scoped);
builder.RegisterComponentInHierarchy<GameOverView>(); builder.Register<IGameOverView, GameOverView>(Lifetime.Scoped);

// Presenters (가능하면 Pure C#)
builder.RegisterEntryPoint<ScorePresenter>();
builder.RegisterEntryPoint<PlusPresenter>();
builder.RegisterEntryPoint<GameOverPresenter>();

// Managers
builder.RegisterComponentInHierarchy<GameManager>();
builder.RegisterComponentInHierarchy<TileManager>();

// 보드 초기화 1회
builder.RegisterBuildCallback(c => {
    var tm = c.Resolve<TileManager>();
    tm.Setup(null, cellSize, originOffset, width, height); // prefab은 팩토리가 관리
});
```

### 풀링
- `PooledTileFactory`가 **생성/회수/위치 지정**을 전담
- 프리팹 인덱스: `idx = clamp(log2(value) - 1)` **예:** 2→0, 4→1, 8→2 …
- `PooledTileTag.poolIndex`로 올바른 풀에 반환

---

## 왜 MVP로 정리했나
- **테스트 가능성**: 모델이 순수 C#이라 단위 테스트 용이
- **치환성/DI**: View/Presenter 인터페이스 주입으로 모킹·교체 간단
- **협업 친화**: 연출은 View, 규칙은 Model로 분리 → 역할 명확
- **확장**: 새로운 UI는 Presenter 추가만으로 스트림에 자연 연결
- **적합성**: Rx 구독 기반 단방향 흐름으로 수명/의존 관계가 명시적

---

## LinkedPool을 선택한 이유
- 연결 리스트 기반이라 **재할당 없이 O(1)** Get/Release (스파이크 완화)
- **튜닝 단순**: `maxSize` 위주 설정 → N×N 보드 변경에도 부담 적음
- `collectionCheck`로 중복/잘못된 Release 조기 검출

> 초기 용량이 엄격히 고정이라면 `ObjectPool<T>`도 적합. 2048처럼 프레임 특정 시점에 Spawn/Release가 몰리는 패턴에는 `LinkedPool<T>`가 더 예측 가능했다.

---

## 빠른 시작
1) **Unity 2022.3.44f1**  
2) 패키지: VContainer, UniRx, UniTask, DOTween 설치  
3) DOTween Setup 실행  
4) 씬에 **GameLifetimeScope** 1개  
   - `tilePrefabs` = (2,4,8,16,…) 순서 할당  
   - `cellSize`, `originOffset`, `width`, `height` 지정  
5) 플레이: 시작에 타일 2개 스폰, 스와이프 시 턴 진행 확인

---

## 구현된 기능
- DPI 보정 스와이프 임계, 턴 중 입력 잠금
- 슬라이드/머지(2048 표준 규칙)
- DOTween 이동/스폰 팝/머지 임팩트
- LinkedPool 기반 타일 풀링
- UniTask await 체인(턴 동기화)
- UniRx 이벤트 스트림으로 UI/로직 연결

---

## 변경 이력(요약)
- **MVP 완성**: Model(순수 C#)/View(연출)/Presenter(중개)로 전면 재구성
- **서드파티**: VContainer, UniRx, UniTask, DOTween
- **입력 UX**: DPI 보정 스와이프, 애니 중 입력 잠금
- **연출**: MovingDOTween, TileFXDOTween, TurnAnimTracker
- **턴 오케스트레이션**: UniTask await 체인
- **풀링/DI**: `IRandomProvider`, `ITileFactory`, `PooledTileFactory(+Tag)`
- **스트림화**: `GameEvents` + UI Presenters

---

## 프로젝트 진행 후 느낀 점

### 1) 싱글톤 위주 → VContainer(DI)
**장점**
- 의존이 **명시적**이고 수명 범위가 분명해짐(전역 상태 누수 감소)
- 프리팹/설정을 **한 곳에서 주입** → 초기화 순서 이슈 감소
- 모듈 교체/테스트가 쉬움

**단점/주의**
- 초반 설정 비용(등록 누락/바인딩 불일치)에 민감
- 작은 기능까지 DI하면 과설계가 되기 쉬움 → “경계” 위주로 적용
- 에디터에선 런타임 주입 특성상 참조가 비어 보일 수 있음

**한줄 총평**: 규모가 커질수록 효과가 커진다. 이 프로젝트에선 이벤트 버스 + 팩토리 + UI Presenter만으로도 충분히 이득.

### 2) 코루틴 → UniTask / 이벤트 → UniRx
**UniTask**
- `await` 체인 가독성, **취소 토큰**으로 턴 중단 처리 확실
- 예외가 호출 스택을 통해 보여서 디버깅 용이
- 주의: `Forget()` 남발, 토큰 전파 누락, 메인스레드 컨텍스트 실수

**UniRx**
- Throttle/Buffer/Delay 등으로 입력/UI 흐름을 **선언적으로** 구성
- 프레임/시간 기반 처리에 강함(중복 입력 방지 등)
- 주의: 구독 해제 누수 → `AddTo(gameObject)`/`CompositeDisposable` 습관화

**한줄 총평**: “턴 진행은 UniTask, 상태/입력은 UniRx” 조합이 역할 분담이 명확하고 유지보수성이 좋았다.
