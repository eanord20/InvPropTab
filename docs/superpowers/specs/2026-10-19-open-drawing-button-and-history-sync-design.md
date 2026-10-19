# Спецификация: кнопка «Открыть чертеж» и синхронизация списков

## Цель

Добавить в панель InvPropTab маленькую кнопку рядом с именем файла, которая открывает связанный чертёж (.idw / .dwg) текущей модели, и исправить ошибку синхронизации исторических списков (поставщики, проекты, разработчики, категории) между одновременно открытыми документами Inventor.

## Текущее состояние

- Имя файла отображается в `_tbFileName` (read-only `TextBox`) без дополнительных действий.
- Рядом с полем «Расположение» уже есть кнопка `📂`, открывающая папку с файлом через `explorer.exe /select`.
- Исторические списки загружаются в каждый экземпляр `PropBrowserControl` при создании и хранятся в памяти. Если в одном документе добавить значение, оно сохраняется в файл, но другие уже открытые экземпляры не перезагружают файл. При последующем сохранении из другого документа старый набор в памяти может затереть только что добавленное значение.

## Требования

### FR-1. Кнопка «Открыть чертеж»

- Рядом с полем имени файла добавить маленькую кнопку (ширина 32, высота равна высоте `TextBox`), аналогичную кнопке «Открыть расположение».
- Кнопка должна искать связанный чертёж в той же папке, где находится текущий документ.
- Приоритет форматов: `.idw`, затем `.dwg`.
- Базовое имя чертежа должно совпадать с базовым именем текущего документа (например, `Part1.ipt` → `Part1.idw`).
- Если найден один подходящий файл — открыть его в Inventor через `Application.Documents.Open`.
- Если чертёж не найден — показать информационное сообщение.
- Если документ не сохранён (нет `FullFileName`) — кнопка неактивна.
- Двойной клик по полю имени файла также открывает связанный чертёж (дополнительный удобный жест).

### FR-2. Синхронизация исторических списков

- Изменения исторических списков (поставщики, проекты, разработчики, категории) должны быть видны во всех открытых документах без перезапуска Inventor.
- Сохранённые в одном документе значения не должны теряться при сохранении/применении из другого открытого документа.
- Решение должно сохранять backward compatibility с текущим форматом `*.json`.

## Архитектура

- `DrawingDocumentService` — новый сервис без состояния, отвечающий за поиск связанного чертежа и его открытие через Inventor API.
- `HistoryStore` — переименованный и доработанный `SupplierStore`, который становится application-level singleton: перед любым чтением перезагружает файл, а запись выполняет атомарно.
- `PropBrowserControl` остаётся UI-слоем: инициализирует кнопку, делегирует логику сервисам, показывает сообщения.

## Детальное проектирование

### `Services/DrawingDocumentService.cs`

```csharp
public static class DrawingDocumentService
{
    public static string? FindRelatedDrawing(string modelPath);
    public static void OpenDrawing(Inventor.Application app, string drawingPath);
}
```

- `FindRelatedDrawing`:
  - Возвращает `null`, если `modelPath` пустой или файл не существует.
  - Получает папку и базовое имя без расширения.
  - Проверяет существование `{base}.idw`, затем `{base}.dwg`.
  - Возвращает первый найденный путь.
- `OpenDrawing`:
  - Проверяет существование файла.
  - Вызывает `app.Documents.Open(drawingPath)`.
  - Обрабатывает COM-исключения (например, чертёж уже открыт).

### `Services/HistoryStore.cs`

Переименовать `SupplierStore` → `HistoryStore`. Изменить поведение:

- Хранить путь к файлу и словарь значений.
- Сделать создание экземпляров централизованным (например, статический словарь `_storesByFileName`), чтобы все `PropBrowserControl` использовали один и тот же объект для одного и того же файла.
- Метод `Load()` всегда читает файл с диска (если файл изменился) и обновляет внутренний список.
- Метод `Save()` атомарно записывает файл (`*.tmp` + `File.Replace`).
- Метод `AddOrUpdate(string value)`:
  - вызывает `Load()` перед изменением,
  - нормализует значение (`Trim`),
  - игнорирует пустые/слишком длинные значения,
  - добавляет без учёта регистра,
  - вызывает `Save()`.

### `UI/PropUI/PropBrowserControl.cs`

- Добавить поля:
  - `private readonly Button _btnOpenDrawing = new Button();`
  - `private readonly ToolTip _toolTip = new ToolTip();`
- В `BuildSinglePageLayout()`:
  - Собрать `fileNamePanel` (Panel с `Dock = Top`): `_tbFileName.Dock = Fill`, `_btnOpenDrawing.Dock = Right`.
  - Заменить `AddUnlabeledControl(_tbFileName)` на `AddUnlabeledControl(fileNamePanel)`.
  - Настроить `_btnOpenDrawing.Text` = символ/иконка, `Width` = 32, высоту равную высоте `_tbFileName`.
  - Установить `toolTip.SetToolTip(_btnOpenDrawing, "Открыть чертёж")`.
- В `WireEvents()`:
  - `_btnOpenDrawing.Click += (_, __) => OpenRelatedDrawing();`
  - `_tbFileName.DoubleClick += (_, __) => OpenRelatedDrawing();`
- Новый метод `OpenRelatedDrawing()`:
  - Получает `doc.FullFileName`.
  - Проверяет, сохранён ли документ.
  - Ищет чертёж через `DrawingDocumentService.FindRelatedDrawing`.
  - Если не найден — `MessageBox.Show("Связанный чертёж не найден.")`.
  - Иначе — `DrawingDocumentService.OpenDrawing(_ctx.App, path)`.
- В `ReloadFromDocument()`:
  - Установить `_btnOpenDrawing.Enabled = !string.IsNullOrWhiteSpace(fullFileName) && System.IO.File.Exists(fullFileName)`.
  - Убрать вызовы `_supplierStore.Load()` и т.п. (перенести загрузку в момент использования — `LoadHistory` / `ManageList`).
- В `LoadHistory(HistoryStore store, ComboBox cb)`:
  - Вызвать `store.Load()` перед заполнением `ComboBox`.
- В `ManageList(...)`:
  - Вызвать `store.Load()` перед открытием формы.
  - После закрытия формы вызвать `store.Save()` (форма сама не сохраняет, только манипулирует объектом).

### `UI/Settings/FieldsSettingsForm.cs` и списковые значения

- При открытии редактора списковых значений перезагружать `fields.json` с диска, чтобы не затирать изменения, внесённые в другом документе.
- Сохранение `fields.json` уже атомарно в `FieldsConfigurationStore.Save`.

## Обработка ошибок

- Все вызовы Inventor API оборачиваются в `try/catch` с логированием через `AppLogger`.
- Если связанный чертёж не найден — информационное сообщение, не ошибка.
- Если открытие чертежа не удалось (например, файл занят) — сообщение с текстом исключения.
- Синхронизация списков не должна падать при повреждённом/отсутствующем json: использовать fallback к пустому списку.

## Тестирование

- Unit-тест для `DrawingDocumentService.FindRelatedDrawing`:
  - временная папка, создание `Part1.ipt` (не обязательно реальный Inventor-файл), создание `Part1.idw` и `Part1.dwg`;
  - проверка приоритета `.idw` над `.dwg`;
  - проверка `null` при отсутствии чертежа.
- Unit-тест для `HistoryStore`:
  - два экземпляра `HistoryStore` с одним и тем же файлом имитируют два открытых документа;
  - первый добавляет значение и сохраняет;
  - второй загружает и видит добавленное значение.
- UI-тест через reflection:
  - проверить наличие `_btnOpenDrawing` в `PropBrowserControl`.

## Файлы для изменения

- Создать: `Services/DrawingDocumentService.cs`
- Переименовать/изменить: `Services/SupplierStore.cs` → `Services/HistoryStore.cs`
- Изменить: `UI/PropUI/PropBrowserControl.cs`
- Изменить: `UI/Settings/FieldsSettingsForm.cs`
- Обновить: `CHANGELOG.md`, `README.md`
- Возможно обновить: `Inv_Property_Tab.csproj` (версия сборки)

## Совместимость

- Имя файла истории `suppliers.json`, `projects.json`, `designers.json`, `categories.json` не меняется.
- Формат json не меняется.
- Переименование класса `SupplierStore` → `HistoryStore` не влияет на внешний интерфейс (COM) или пользовательские данные.
