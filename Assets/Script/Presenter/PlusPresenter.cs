using UniRx;

using UnityEngine;

using VContainer;

public sealed class PlusPresenter : MonoBehaviour
{
	PlusModel model;
	PlusView view;
	CompositeDisposable cd;

	[Inject]
	public void Construct(PlusModel model, PlusView view)
	{
		this.model = model;
		this.view = view;
	}

	void OnEnable()
	{
		if (model == null || view == null) return;
		cd = new CompositeDisposable();

		model.Delta
			.Subscribe(view.Show)
			.AddTo(cd);
	}

	void OnDisable() => cd?.Dispose();
}
