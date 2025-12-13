using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;
using Robust.Shared.Utility;

namespace Content.Shared._Metro14.NpcTrader;

[Prototype("npcTraderItemForCatalog")]
public sealed partial class NpcTraderItemForCatalogPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [ParentDataField(typeof(PrototypeIdArraySerializer<NpcTraderItemForCatalogPrototype>))]
    public string[]? Parents { get; private set; }

    [DataField(required: true)]
    public string Name { get; set; } = default!;

    [DataField("description")]
    public string Description = "";

    [AbstractDataField]
    [NeverPushInheritance]
    public bool Abstract { get; private set; }

    [DataField("iconId")]
    public string IconId = ""; // указывается id прототипа, с которого нужно перетащить иконку.

    [DataField("iconPath")]
    public string IconPath = ""; // указывается путь к png изображению иконки.

    [DataField("givingItems")]
    public Dictionary<string, int> GivingItems = new Dictionary<string, int>(); // стоимость товара

    [DataField("timeRespawn")]
    public float TimeRespawn = 600; // скорость появления товара в магазине

    [DataField("canRespawn")]
    public bool CanRespawn = false; // предмет может вновь появится в магазине

    [DataField("countRespawn")]
    public int CountRespawn = 0; // количество появляющихся товаров в магазине через заданный промежуток времени. Если -1 до восполняется до максимума

    [DataField("takingItems")]
    public Dictionary<string, int> TakingItems = new Dictionary<string, int>(); // сам товар

    [DataField("productTags")]
    public List<string> ProductTags = new List<string>(); // теги товара для фильтрации

}
