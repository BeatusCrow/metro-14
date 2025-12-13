using Content.Shared.UserInterface;
using Content.Server.Advertise.EntitySystems;
using Content.Shared.Advertise.Components;
using Content.Shared.Arcade;
using Content.Shared.Power;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Robust.Server.GameObjects;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Storage;
using Robust.Shared.Containers;
using Content.Shared._Metro14.NpcTrader;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Content.Shared.Inventory;
using Content.Shared.Weapons.Ranged.Components;
using Content.Server.Chat.Systems;
using Content.Shared.Chat;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;

namespace Content.Server._Metro14.NpcTrader;

public sealed class NpcTraderSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly SharedHandsSystem _handSystem = default!;
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;

    private HashSet<EntityUid> _entitiesInRange = new();
    private List<EntityUid> _delItem = new List<EntityUid>();
    private static readonly Random _random = new Random();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NpcTraderComponent, ComponentInit>(OnComponentInit);
        Subs.BuiEvents<NpcTraderComponent>(NpcTraderUiKey.Key, subs =>
        {
            subs.Event<NpcTraderBuyMessage>(OnNpcTraderBuy);
        });
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var npcTraders = EntityQueryEnumerator<NpcTraderComponent>();
        while (npcTraders.MoveNext(out var uid, out var component))
        {
            if (_gameTiming.CurTime > component.NextTick)
            {
                component.NextTick = _gameTiming.CurTime + TimeSpan.FromSeconds(component.DeltaTime);

                if (component.CopyItemsInCatalog == null || component.CopyItemsInCatalog.Count == 0)
                    continue;

                if (component.RespawnItems.Count != null && component.RespawnItems.Count != 0)
                {
                    foreach (var itemForRespawn in component.RespawnItems)
                    {
                        if (_gameTiming.CurTime > itemForRespawn.Value.TimeRespawn)
                        {
                            if (itemForRespawn.Value.CountOfItems == -1)
                            {
                                component.ItemsInCatalog[itemForRespawn.Key] = component.CopyItemsInCatalog[itemForRespawn.Key];
                                Dirty(uid, component);
                            }
                            else
                            {
                                if (component.ItemsInCatalog[itemForRespawn.Key] + itemForRespawn.Value.CountOfItems <= component.CopyItemsInCatalog[itemForRespawn.Key])
                                {
                                    component.ItemsInCatalog[itemForRespawn.Key] += itemForRespawn.Value.CountOfItems;
                                    Dirty(uid, component);
                                }    
                                else
                                {
                                    component.ItemsInCatalog[itemForRespawn.Key] = component.CopyItemsInCatalog[itemForRespawn.Key];
                                    Dirty(uid, component);
                                }
                            }
                        }
                    }
                }

                foreach (var item in component.ItemsInCatalog)
                {
                    if (!_prototype.TryIndex<NpcTraderItemForCatalogPrototype>(item.Key, out var product))
                        continue;

                    if (!product.CanRespawn)
                        continue;

                    if (component.ItemsInCatalog[item.Key] != component.CopyItemsInCatalog[item.Key])
                    {
                        if (!component.RespawnItems.ContainsKey(item.Key))
                            component.RespawnItems.Add(item.Key, (
                                TimeRespawn: _gameTiming.CurTime + TimeSpan.FromSeconds(product.TimeRespawn),
                                CountOfItems: product.CountRespawn
                            ));
                    }
                }
            }
        }
    }

    /// <summary>
    /// Стандартный метод инициализации компонента.
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="npcTraderComponent"></param>
    /// <param name="args"></param>
    private void OnComponentInit(EntityUid uid, NpcTraderComponent npcTraderComponent, ComponentInit args)
    {
        // проверяем, что к нему привязаны хоть какие-то каталоги
        if (npcTraderComponent.ItemsInCatalog.Count <= 0)
        {
            // дальше перебираем все привязанные каталоги
            foreach (var nameOfCatalog in npcTraderComponent.Catalog)
            {
                // проверяем, что указанные каталоги корректны
                if (!_prototype.TryIndex<NpcTraderSalesCatalogPrototype>(nameOfCatalog, out var typeOfCatalog))
                    continue;

                // перебираем все предложения в данном каталоге 
                foreach (var nameOfItem in typeOfCatalog.Catalog)
                {
                    // проверяем, что указанные предложения существуют
                    if (!_prototype.TryIndex<NpcTraderItemForCatalogPrototype>(nameOfItem.Key, out var prototypeItemOfCatalog))
                        continue;

                    // помещаем в словарь компонента торговца все незаписанные раннее предложения 
                    if (!npcTraderComponent.ItemsInCatalog.ContainsKey(prototypeItemOfCatalog.ID))
                        npcTraderComponent.ItemsInCatalog.Add(prototypeItemOfCatalog.ID, nameOfItem.Value);
                }
            }
        }

        npcTraderComponent.CopyItemsInCatalog = new Dictionary<string, int>(npcTraderComponent.ItemsInCatalog);
        npcTraderComponent.NextTick = _gameTiming.CurTime + TimeSpan.FromSeconds(npcTraderComponent.DeltaTime);
        Dirty(uid, npcTraderComponent);
    }


    /// <summary>
    /// Метод, вызывающийся при нажатии кнопки "купить" на клиентской части.
    /// </summary>
    /// <param name="uid"> торговец</param>
    /// <param name="component"> компонент торговца </param>
    /// <param name="args"> параметры сообщения | args.Buyer - NetEntity покупателя | args.ProductId - ID продукта </param>
    public void OnNpcTraderBuy(EntityUid uid, NpcTraderComponent component, NpcTraderBuyMessage args)
    {
        _delItem.Clear();

        // проверяем, что получили корректный ID предложения
        if (!component.ItemsInCatalog.ContainsKey(args.ProductId))
            return;

        // елси данное предложение не является бесконечным, то нужно проверить, не закончился ли товар
        if (component.ItemsInCatalog[args.ProductId] != -1)
        {
            if (component.ItemsInCatalog[args.ProductId] == 0)
            {
                TrySayPhrase(uid, component.PhrasesNoProduct[_random.Next(component.PhrasesNoProduct.Count)]);
                return;
            }
        }

        if (!_prototype.TryIndex<NpcTraderItemForCatalogPrototype>(args.ProductId, out var npcTraderItemForCatalogProto))
            return;

        bool tempFlag = false;
        foreach (var givingItem in npcTraderItemForCatalogProto.GivingItems)
        {
            if (!_prototype.TryIndex<EntityPrototype>(givingItem.Key, out var tempProto))
                continue;

            if (givingItem.Value > 0)
            {
                for (int i = 0; i < givingItem.Value; i++)
                {
                    if (!TryFindItem(uid, givingItem.Key, _entityManager.GetEntity(args.Buyer)))
                    {
                        tempFlag = true;
                        break;
                    }
                }
            }
        }

        if (!tempFlag)
        {
            TryDeleteItems();
            TryGiveItems(uid, args.ProductId, _entityManager.GetEntity(args.Buyer));
            tempFlag = false;
        }
        else
        {
            TrySayPhrase(uid, component.PhrasesLittleMoney[_random.Next(component.PhrasesLittleMoney.Count)]);
        }
    }

    private void TrySayPhrase(EntityUid npcTrader, string text)
    {
        var chatSystem = _entityManager.EntitySysManager.GetEntitySystem<ChatSystem>();

        // Воспроизводим фразу от имени сущности  
        chatSystem.TrySendInGameICMessage(
            npcTrader,
            Loc.GetString(text),
            InGameICChatType.Speak,
            hideChat: false,  // скрыть из чата  
            hideLog: false    // скрыть из логов  
        );
    }

    private void TryDeleteItems()
    {
        foreach(var item in _delItem)
        {
            QueueDel(item);
        }
    }

    private void TryGiveItems(EntityUid npcUid, string productId, EntityUid playerUid)
    {
        if (!_prototype.TryIndex<NpcTraderItemForCatalogPrototype>(productId, out var tradeItemComp))
            return;

        if (tradeItemComp.TakingItems == null || tradeItemComp.TakingItems.Count == 0)
            return;

        foreach(var itemId in tradeItemComp.TakingItems)
        {
            if (itemId.Value > 0)
                for (int i = 0; i < itemId.Value; i++)
                    _inventory.SpawnItemOnEntity(playerUid, itemId.Key);
        }

        if (_entityManager.TryGetComponent(npcUid, out NpcTraderComponent? npcTraderComponent))
        {
            if (npcTraderComponent.ItemsInCatalog.ContainsKey(productId) && npcTraderComponent.ItemsInCatalog[productId] != -1)
            {
                npcTraderComponent.ItemsInCatalog[productId] -= 1;
                Dirty(npcUid, npcTraderComponent);
            }

            TrySayPhrase(npcUid, npcTraderComponent.PhrasesThankYou[_random.Next(npcTraderComponent.PhrasesThankYou.Count)]);
        }

        _adminLogger.Add(LogType.Action, LogImpact.Low, $"Игрок {playerUid} купил '{productId}'");
    }

    public bool TryFindItem(EntityUid npcUid, string itemPrice, EntityUid buyer)
    {
        // Проверяем руки на наличие предмета-оплаты
        if (_entityManager.TryGetComponent(buyer, out HandsComponent? handsComponent))
        {
            foreach (var hand in handsComponent.Hands.Keys)
            {
                var tempHoldItem = _handSystem.GetHeldItem(buyer, hand);

                if (tempHoldItem != null)
                {
                    if (_entityManager.TryGetComponent(tempHoldItem, out StorageComponent? storageCmp))
                        if (TryFindEntityInStorage(storageCmp, itemPrice))
                            return true;

                    if (!TryComp<MetaDataComponent>(tempHoldItem, out var metaData))
                        continue;

                    var prototypeId = metaData.EntityPrototype?.ID;
                    if (prototypeId == null)
                        continue;

                    if (!_prototype.TryIndex<EntityPrototype>(prototypeId, out var tempProto))
                        continue;

                    if (tempProto.ID.Equals(itemPrice))
                    {
                        if (_delItem.Contains((EntityUid)tempHoldItem))
                            continue;

                        return CheckCartridgeComp((EntityUid)tempHoldItem);    
                    }
                }
            }
        }

        // теперь ищем в карманах, на поясе, спине или в рюкзаке
        var slotEnumerator = _inventory.GetSlotEnumerator(buyer);
        while (slotEnumerator.NextItem(out var item, out var slot))
        {
            if (!_entityManager.TryGetComponent(item, out StorageComponent? storageComponent))
            {
                if (!TryComp<MetaDataComponent>(item, out var _metaData))
                    continue;

                var protoId = _metaData.EntityPrototype?.ID;
                if (protoId == null)
                    continue;

                if (!_prototype.TryIndex<EntityPrototype>(protoId, out var tmpProto))
                    continue;

                if (tmpProto.ID.Equals(itemPrice))
                {
                    if (_delItem.Contains((EntityUid)item))
                        continue;

                    return CheckCartridgeComp((EntityUid)item);
                }
            }
            else
            {
                if (storageComponent == null) //TODO: вещи выдаются только по 1 штуке, вместо указанного количества :(
                    continue;

                if (TryFindEntityInStorage(storageComponent, itemPrice))
                    return true;
            }
        }

        // if we didn't find anything on ourselves, we look for something nearby
        _entitiesInRange.Clear();
        var Coordinates = _entityManager.GetComponent<TransformComponent>(npcUid).Coordinates;
        _entityLookup.GetEntitiesInRange(Coordinates, 1, _entitiesInRange, flags: LookupFlags.Uncontained);

        foreach (var nearEntity in _entitiesInRange)
        {
            if (_entityManager.TryGetComponent(nearEntity, out StorageComponent? storageComp))
                if (TryFindEntityInStorage(storageComp, itemPrice))
                    return true;

            if (!TryComp<MetaDataComponent>(nearEntity, out var _meta))
                continue;

            var protoID = _meta.EntityPrototype?.ID;
            if (protoID == null)
                continue;

            if (!_prototype.TryIndex<EntityPrototype>(protoID, out var tmpProt))
                continue;

            if (tmpProt.ID.Equals(itemPrice))
            {
                if (_delItem.Contains((EntityUid)nearEntity))
                    continue;

                return CheckCartridgeComp((EntityUid)nearEntity);
            }
        }

        return false;
    }

    private bool TryFindEntityInStorage(StorageComponent storageComp, string itemPrice)
    {
        foreach (var storageItem in storageComp.StoredItems) // проверяем рюкзак
        {
            if (_entityManager.TryGetComponent(storageItem.Key, out StorageComponent? storageComponent))
                TryFindEntityInStorage(storageComponent, itemPrice);

            if (!TryComp<MetaDataComponent>(storageItem.Key, out var meta))
                continue;

            var tempPrototypeId = meta.EntityPrototype?.ID;
            if (tempPrototypeId == null)
                continue;

            if (!_prototype.TryIndex<EntityPrototype>(tempPrototypeId, out var tempProt))
                continue;

            if (tempProt.ID.Equals(itemPrice))
            {
                if (_delItem.Contains((EntityUid)storageItem.Key))
                    continue;

                return CheckCartridgeComp((EntityUid)storageItem.Key);
            }
        }

        return false;
    }

    private bool CheckCartridgeComp(EntityUid uid)
    {
        if (TryComp<CartridgeAmmoComponent>(uid, out var cartridge))
        {
            if (!cartridge.Spent)
            {
                _delItem.Add(uid);
                return true;
            }
            else
            {
                return false;
            }
        }
        else
        {
            _delItem.Add(uid);
            return true;
        }
    }
}
