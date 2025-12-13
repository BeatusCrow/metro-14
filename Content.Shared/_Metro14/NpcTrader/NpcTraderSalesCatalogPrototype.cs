using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;
using Robust.Shared.Utility;

namespace Content.Shared._Metro14.NpcTrader;

[Prototype("npcTraderSalesCatalog")]
public sealed partial class NpcTraderSalesCatalogPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [ParentDataField(typeof(PrototypeIdArraySerializer<NpcTraderSalesCatalogPrototype>))]
    public string[]? Parents { get; private set; }

    [AbstractDataField]
    [NeverPushInheritance]
    public bool Abstract { get; private set; }

    [DataField("catalog")]
    public Dictionary<string, int> Catalog = new Dictionary<string, int>(); /// указываются NpcTraderItemForCatalogPrototype и количество предметов
                                                                            /// Если указан -1, то количество неограничено

}
