using UnityEngine;
using VContainer;
using VContainer.Unity;

public class GameLifetimeScope : LifetimeScope
{
    [Header("Bindings (����)")]
    [SerializeField] GameManager gameManager;
    [SerializeField] TileManager tileManager;

    [Header("Tile Prefabs (2,4,8,... ����)")]
    [SerializeField] GameObject[] tilePrefabs;

    [Header("Layout (���丮���� ��ǥ�� ��꿡 ���)")]
    [SerializeField] Vector2 cellSize = new(1.2f, 1.2f);
    [SerializeField] Vector2 originOffset = new(-1.8f, -1.8f);

	protected override void Configure(IContainerBuilder builder)
	{
		builder.Register<IRandomProvider, SystemRandomProvider>(Lifetime.Scoped);
		builder.Register<ITileFactory, PooledTileFactory>(Lifetime.Scoped)
			   .WithParameter("prefabs", tilePrefabs)
			   .WithParameter("cellSize", cellSize)
			   .WithParameter("originOffset", originOffset)
			   .WithParameter("maxSize", 256)
			   .WithParameter("collectionCheck", false);

		// Scene Components ���� (Drag&Drop�� �� �ߴٸ� Hierarchy���� �ڵ� Ž��)
		if (gameManager != null) 
			builder.RegisterComponent(gameManager);
		else
			builder.RegisterComponentInHierarchy<GameManager>();
		
		if (tileManager != null)
			builder.RegisterComponent(tileManager);
		else
			builder.RegisterComponentInHierarchy<TileManager>();
	}
}