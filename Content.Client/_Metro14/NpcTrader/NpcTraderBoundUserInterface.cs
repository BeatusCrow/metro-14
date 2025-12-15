using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Content.Shared._Metro14.NpcTrader;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Nuke;

namespace Content.Client._Metro14.NpcTrader
{
    [UsedImplicitly]
    public sealed class NpcTraderBoundUserInterface : BoundUserInterface
    {
        [ViewVariables]
        private NpcTraderMenu? _menu;

        public NpcTraderBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

        protected override void Open()
        {
            base.Open();

            _menu = this.CreateWindow<NpcTraderMenu>();
            _menu.SetEntity(Owner);
            _menu.OpenCentered();
            _menu.OnClose += Close;


            _menu.OnBuyButtonPressed += OnBuyButtonPressed;
        }

        private void OnBuyButtonPressed(NetEntity buyer, string productId)
        {
            SendMessage(new NpcTraderBuyMessage(buyer, productId));
        }

        protected override void UpdateState(BoundUserInterfaceState state)
        {
            base.UpdateState(state);

            if (_menu == null)
                return;
        }
    }
}
