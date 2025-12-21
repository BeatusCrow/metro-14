using Content.Shared._Metro14.BoomBox;
using JetBrains.Annotations;
using Robust.Client.GameObjects;

namespace Content.Client._Metro14.BoomBox.UI;

[UsedImplicitly]
public sealed class BoomBoxBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private BoomBoxWindow? _window;

    public BoomBoxBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = new BoomBoxWindow();
        _window.OpenCentered();

        _window.OnClose += Close;
        _window.MinusVolButtonPressed += OnMinusVolButtonPressed;
        _window.PlusVolButtonPressed += OnPlusVolButtonPressed;
        _window.StartButtonPressed += OnStartButtonPressed;
        _window.StopButtonPressed += OnStopButtonPressed;
    }

    private void OnMinusVolButtonPressed()
    {
        SendMessage(new BoomBoxMinusVolMessage());
    }

    private void OnPlusVolButtonPressed()
    {
        SendMessage(new BoomBoxPlusVolMessage());
    }

    private void OnStartButtonPressed()
    {
        SendMessage(new BoomBoxStartMessage());
    }

    private void OnStopButtonPressed()
    {
        SendMessage(new BoomBoxStopMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_window == null || state is not BoomBoxUiState cast)
            return;

        _window.UpdateState(cast);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _window?.Dispose();
    }
}
