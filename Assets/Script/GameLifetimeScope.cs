using UnityEngine;
using VContainer;
using VContainer.Unity;

public class GameLifetimeScope : LifetimeScope
{
    [Header("Bindings (선택)")]
    [SerializeField] GameManager gameManager;
    [SerializeField] TileManager tileManager;

    [Header("Tile Prefabs (2,4,8,... 순서)")]
    [SerializeField] GameObject[] tilePrefabs;

    [Header("Layout (팩토리에서 좌표계 계산에 사용)")]
    [SerializeField] Vector2 cellSize = new(1.2f, 1.2f);
    [SerializeField] Vector2 originOffset = new(-1.8f, -1.8f);
	[SerializeField] int width = 4;
	[SerializeField] int height = 4;

	protected override void Configure(IContainerBuilder builder)
	{
		builder.Register<IRandomProvider, SystemRandomProvider>(Lifetime.Scoped);
		builder.Register<ITileFactory, PooledTileFactory>(Lifetime.Scoped)
			   .WithParameter("prefabs", tilePrefabs)
			   .WithParameter("cellSize", cellSize)
			   .WithParameter("originOffset", originOffset)
			   .WithParameter("maxSize", 256)
			   .WithParameter("collectionCheck", false);

		builder.RegisterBuildCallback(c =>
		{
			var tm = c.Resolve<TileManager>();
			tm.Setup(cellSize, originOffset, width, height);
		});

		// Scene Components 주입 (Drag&Drop을 안 했다면 Hierarchy에서 자동 탐색)
		if (gameManager != null) 
			builder.RegisterComponent(gameManager);
		else
			builder.RegisterComponentInHierarchy<GameManager>();
		
                if (tileManager != null)
                        builder.RegisterComponent(tileManager);
                else
                        builder.RegisterComponentInHierarchy<TileManager>();

		builder.Register<GameEvents>(Lifetime.Singleton);
		builder.Register<ScoreModel>(Lifetime.Singleton);
		builder.Register<PlusModel>(Lifetime.Singleton);
		builder.Register<GameOverModel>(Lifetime.Singleton);
		builder.RegisterComponentInHierarchy<DebugEventLogger>();
		builder.RegisterComponentInHierarchy<GameOverPresenter>();
		builder.RegisterComponentInHierarchy<ScorePresenter>();
		builder.RegisterComponentInHierarchy<PlusPresenter>();
		builder.RegisterComponentInHierarchy<GameOverView>();
		builder.RegisterComponentInHierarchy<ScoreView>();
		builder.RegisterComponentInHierarchy<PlusView>();

	}
}