# Скины снарядов

В деревне справа под бестиарием находится кнопка **«СКИНЫ СНАРЯДОВ»**.
Каждый облик стоит **30 бриллиантов**, покупается один раз и сразу выбирается.
Купленный скин можно бесплатно надевать и снимать; обычные облики доступны всегда.
Покупки и выбор сохраняются в PlayerPrefs отдельно от улучшений текущего боя.
Источники бриллиантов пока не добавлены по решению пользователя.

| Снаряд | Скин | Изображение |
|---|---|---|
| Светлячок | Аврора | `Assets/Art/ProjectileSkins/firefly-aurora.png` |
| Ёж | Солнечный | `Assets/Art/ProjectileSkins/hedgehog-sunburst.png` |
| Кролик | Карамельный | `Assets/Art/ProjectileSkins/rabbit-candy.png` |
| Медоед | Ультрафиолет | `Assets/Art/ProjectileSkins/honey-badger-ultraviolet.png` |
| Кабан | Алый гром | `Assets/Art/ProjectileSkins/boar-scarlet.png` |
| Бобёр | Лагуна | `Assets/Art/ProjectileSkins/beaver-lagoon.png` |
| Лягушка | Тропический | `Assets/Art/ProjectileSkins/frog-tropical.png` |
| Белка | Закатная | `Assets/Art/ProjectileSkins/squirrel-sunset.png` |

Изображения созданы встроенным **image_gen**, каждый отдельным редактированием своего исходного спрайта. Полные запросы: [ProjectileSkinPrompts.md](ProjectileSkinPrompts.md).

## Редактируемые данные

- Каталог и цены: `Assets/Resources/ProjectileSkins/Catalogue.asset`.
- Интерфейс: `Assets/Resources/UI/ProjectileSkinShop.prefab`, экземпляр в `Assets/Scenes/UIscene.unity`.
- На восьми префабах снарядов установлен `ProjectileSkinVisual` с индексами 0–7. У ёжика и кролика используется унаследованный компонент базового префаба с собственным индексом.
- Выбранные изображения используются в бою, на колесе выбора и в интерфейсе улучшений. Копии кролика наследуют облик; лягушка сохраняет его после посадки на врага.
- Импорт сохраняет размер исходного спрайта в игровых единицах и его центр вращения.
- `ProjectileSkinMenuSync` обновляет уже открытую в редакторе старую копию меню после импорта и перед Play Mode: добавляет только отсутствующие кнопку и экземпляр префаба. Сцена не перезагружается; несохранённые правки остаются несохранёнными.

## Проверка

`ProjectileSkinValidation.RunBatch` запускается в отдельной копии проекта. Проверяются нехватка валюты, некорректные запросы, покупка каждого скина, защита от повторной оплаты, бесплатное переключение, сохранение после загрузки сцены, родные снаряды и их изображения, урон, HP и параметры коллайдеров.

Отчёт: [ProjectileSkinValidationReport.txt](ProjectileSkinValidationReport.txt).
Снимки: [деревня](ProjectileSkinVillage.png), [верх магазина](ProjectileSkinShopTop.png), [низ магазина](ProjectileSkinShopBottom.png).

Восстановление старой открытой сцены отдельно проверено через `ProjectileSkinMenuSync.RunBatch`: установка и сохранение в чистой сцене, отсутствие дублей, сохранность несохранённых правок, редактируемый экземпляр префаба. [Отчёт](ProjectileSkinMenuSyncValidation.txt).
