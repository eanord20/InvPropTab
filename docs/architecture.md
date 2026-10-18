# Архитектура

## Точка входа

`AddInServer` реализует `Inventor.ApplicationAddInServer`, подписывается на активацию и сохранение документа и управляет временем жизни панели.

## Панель

`PropBrowserPaneManager` создаёт `BrowserPane` и размещает COM-visible `PropBrowserActiveX`. Внутри него работает WinForms `PropBrowserControl`.

`ConfigurableFieldsPanel` строит редакторы по `FieldsConfiguration`. `FieldEditorFactory` создаёт TextBox, ComboBox и read-only редакторы. `FieldEditorBinding` хранит исходное значение, dirty-состояние, цвет и список вариантов.

## Конфигурация

`InventorPropertyCatalog` содержит поддерживаемые свойства. `FieldsConfigurationStore` загружает и атомарно сохраняет `%APPDATA%\InvPropTab\fields.json`. При отсутствии файла используется каталог по умолчанию.

## Inventor API

`InventorPropertyAccessor` и `IPropertiesService` читают и записывают iProperties. Временные COM RCW освобождаются через `ComRelease` в `finally`. Ошибки записываются через `AppLogger`.

## Установщик

Inno Setup устанавливает DLL для текущего пользователя, создаёт UTF-8-совместимый manifest и регистрирует два COM-класса в `HKCU\Software\Classes`.
