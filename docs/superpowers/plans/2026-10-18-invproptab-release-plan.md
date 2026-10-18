# InvPropTab Release Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Стабилизировать add-in Inventor 2020, отключить историю Description, добавить декларативно настраиваемые поля, установщик Inno Setup и подготовить публичный MIT-репозиторий.

**Architecture:** Поля описываются версионированной JSON-конфигурацией и создаются фабрикой WinForms-редакторов. Доступ к Inventor изолируется сервисом с явным управлением COM-ссылками. Установка выполняется для текущего пользователя с per-user COM-регистрацией.

**Tech Stack:** C#, .NET Framework 4.8, WinForms, Autodesk Inventor 2020 Interop, MSTest или существующий совместимый test framework после проверки окружения, Inno Setup 6, GitHub.

## Global Constraints

- Поддерживается только Autodesk Inventor 2020 x64.
- Установка не требует прав администратора.
- `Description` не имеет истории и автодополнения.
- Конфигурация хранится в `%APPDATA%\InvPropTab\fields.json`.
- Репозиторий публичный, лицензия MIT.
- Autodesk DLL, пользовательские данные и результаты сборки не публикуются.
- Все принадлежащие add-in COM-объекты освобождаются в `finally`.

---

### Task 1: Базовая подготовка Git-репозитория

**Files:**
- Create: `.gitignore`
- Create: `LICENSE`
- Create: `README.md`
- Modify: `Inv_Property_Tab/Inv_Property_Tab/Inv_Property_Tab.csproj`

- [ ] Инициализировать Git только после явного подтверждения пользователя: `git init`.
- [ ] Добавить правила исключения `.vs/`, `bin/`, `obj/`, `*.user`, `*.log`, локальных JSON и Autodesk interop DLL.
- [ ] Добавить полный стандартный текст MIT с выбранным владельцем copyright.
- [ ] Убрать автоматическое развёртывание из обычного `Build`; сохранить отдельную явно вызываемую цель разработки только для Inventor 2020.
- [ ] Параметризовать путь к `Autodesk.Inventor.Interop.dll` свойством MSBuild.
- [ ] Выполнить `dotnet build ... -c Release` с отключённым deploy-target; ожидается успешная сборка без копирования в `%APPDATA%`.
- [ ] Проверить `git status` и убедиться, что бинарники и пользовательские файлы не индексируются.
- [ ] Зафиксировать отдельным коммитом подготовку репозитория.

### Task 2: Создать тестовый контур без зависимости от Inventor

**Files:**
- Create: `Inv_Property_Tab/Inv_Property_Tab.Tests/Inv_Property_Tab.Tests.csproj`
- Create: `Inv_Property_Tab/Inv_Property_Tab.Tests/Configuration/FieldsConfigurationStoreTests.cs`
- Modify: `Inv_Property_Tab/Inv_Property_Tab.slnx`

**Interfaces:**
- Tests consume: `FieldsConfigurationStore`, `FieldsConfigurationValidator` из Task 3.

- [ ] Проверить доступный test framework, совместимый с `net48`; не добавлять пакет моложе семи дней.
- [ ] Создать тестовый проект без ссылки на Autodesk interop.
- [ ] Добавить временный failing test загрузки конфигурации по умолчанию.
- [ ] Запустить тест и подтвердить ожидаемый FAIL из-за отсутствующих типов.
- [ ] Зафиксировать только тестовый каркас отдельным коммитом.

### Task 3: Модель и хранилище конфигурации

**Files:**
- Create: `Inv_Property_Tab/Inv_Property_Tab/Configuration/FieldDefinition.cs`
- Create: `Inv_Property_Tab/Inv_Property_Tab/Configuration/FieldsConfiguration.cs`
- Create: `Inv_Property_Tab/Inv_Property_Tab/Configuration/FieldsConfigurationValidator.cs`
- Create: `Inv_Property_Tab/Inv_Property_Tab/Configuration/FieldsConfigurationStore.cs`
- Create: `Inv_Property_Tab/Inv_Property_Tab/Configuration/default-fields.json`
- Test: `Inv_Property_Tab/Inv_Property_Tab.Tests/Configuration/FieldsConfigurationStoreTests.cs`

**Interfaces:**
- `FieldsConfigurationStore.Load(): FieldsConfiguration`
- `FieldsConfigurationStore.Save(FieldsConfiguration configuration): void`
- `FieldsConfigurationValidator.Validate(FieldsConfiguration configuration): IReadOnlyList<string>`

- [ ] Написать failing tests для версии 1, уникальности `id`, обязательной подписи, допустимых source/editor, стабильной сортировки и неизвестных JSON-полей.
- [ ] Написать failing tests для отсутствующего, повреждённого и корректного `%APPDATA%`-файла с подменяемым корневым каталогом тестов.
- [ ] Реализовать неизменяемые строковые константы source/editor и модели конфигурации.
- [ ] Реализовать валидатор без зависимости от UI и Inventor.
- [ ] Реализовать атомарную запись: временный файл, повторное чтение, замена целевого файла.
- [ ] Добавить заводские поля: Part Number, Description, Category, Designer, Project, Supplier, Mass, Dimensions и Custom Properties.
- [ ] Для Description установить `editor = text`, `rememberValues = false`.
- [ ] Запустить весь тестовый проект; ожидается PASS.
- [ ] Зафиксировать конфигурационную подсистему отдельным коммитом.

### Task 4: Исправить сохранение Description

**Files:**
- Modify: `Inv_Property_Tab/Inv_Property_Tab/UI/PropUI/PropBrowserControl.cs`
- Modify: `Inv_Property_Tab/Inv_Property_Tab/Services/SupplierStore.cs`
- Test: `Inv_Property_Tab/Inv_Property_Tab.Tests/Configuration/FieldsConfigurationStoreTests.cs`

- [ ] Добавить тест, что заводское поле Description не допускает историю.
- [ ] Удалить `_descriptionStore`, кнопку управления описаниями и загрузку `descriptions.json`.
- [ ] До полного перехода на динамический UI заменить Description на обычный `TextBox` и применять через текстовый путь.
- [ ] Не добавлять код удаления существующего `%APPDATA%\InvPropTab\descriptions.json`.
- [ ] Выполнить тесты и Release-сборку.
- [ ] Вручную проверить в Inventor, что новое Description сохраняется в iProperty, но не появляется в списке после перезапуска.
- [ ] Зафиксировать исправление отдельным коммитом.

### Task 5: Изолировать Inventor API и стабилизировать COM lifecycle

**Files:**
- Create: `Inv_Property_Tab/Inv_Property_Tab/Services/IPropertyAccessor.cs`
- Create: `Inv_Property_Tab/Inv_Property_Tab/Services/InventorPropertyAccessor.cs`
- Create: `Inv_Property_Tab/Inv_Property_Tab/Interop/ComRelease.cs`
- Modify: `Inv_Property_Tab/Inv_Property_Tab/Services/IPropertiesService.cs`
- Modify: `Inv_Property_Tab/Inv_Property_Tab/UI/PropBrowserPaneManager.cs`
- Modify: `Inv_Property_Tab/Inv_Property_Tab/StandardAddInServer.cs`

**Interfaces:**
- `IPropertyAccessor.Read(Document doc, FieldDefinition field): string`
- `IPropertyAccessor.Write(Document doc, FieldDefinition field, string value): PropertyWriteResult`
- `ComRelease.Final(object value): void`

- [ ] Найти и перечислить все получаемые COM-ссылки и определить владельца каждой.
- [ ] Написать unit tests результата записи через тестовый accessor без Inventor.
- [ ] Реализовать доступ к PropertySet прежде всего по внутреннему идентификатору, локализованное имя оставить fallback.
- [ ] Освобождать временные коллекции и элементы в `finally`, не освобождая объекты, которыми владеет Inventor или другой компонент.
- [ ] Подписаться на закрытие документов и удалять соответствующий `BrowserPane` из кэша.
- [ ] Освободить кэш и события при `Deactivate`; убрать `GC.Collect()` как механизм управления COM.
- [ ] Вернуть фактический успех/ошибку записи вместо безусловного `true`.
- [ ] Проверить циклы открытия/закрытия не менее 20 документов и завершение процесса Inventor.
- [ ] Зафиксировать COM-стабилизацию отдельным коммитом.

### Task 6: Динамическая панель полей

**Files:**
- Create: `Inv_Property_Tab/Inv_Property_Tab/UI/Fields/FieldEditorBinding.cs`
- Create: `Inv_Property_Tab/Inv_Property_Tab/UI/Fields/FieldEditorFactory.cs`
- Create: `Inv_Property_Tab/Inv_Property_Tab/UI/Fields/ConfigurableFieldsPanel.cs`
- Modify: `Inv_Property_Tab/Inv_Property_Tab/UI/PropUI/PropBrowserControl.cs`

**Interfaces:**
- `FieldEditorFactory.Create(FieldDefinition definition): FieldEditorBinding`
- `ConfigurableFieldsPanel.Load(Document document): void`
- `ConfigurableFieldsPanel.Apply(Document document): IReadOnlyList<PropertyWriteResult>`

- [ ] Написать tests для выбора редактора `text`, `multiline`, `list`, `number`, `readonly` без запуска Inventor.
- [ ] Реализовать binding, содержащий определение, WinForms-контрол, исходное значение и методы чтения/записи UI.
- [ ] Реализовать фабрику редакторов и валидацию числового значения без изменения культуры документа.
- [ ] Построить панель из видимых полей, отсортированных по `order`, затем `id`.
- [ ] Перевести существующие основные поля на конфигурацию, сохранив системные поля только для чтения.
- [ ] Показывать итоговый список полей, которые не удалось записать.
- [ ] Выполнить unit tests, Release-сборку и ручную проверку в Inventor.
- [ ] Зафиксировать динамическую панель отдельным коммитом.

### Task 7: Окно полной настройки полей

**Files:**
- Create: `Inv_Property_Tab/Inv_Property_Tab/UI/Settings/FieldsSettingsForm.cs`
- Create: `Inv_Property_Tab/Inv_Property_Tab/UI/Settings/FieldEditForm.cs`
- Modify: `Inv_Property_Tab/Inv_Property_Tab/UI/PropUI/PropBrowserControl.cs`
- Test: `Inv_Property_Tab/Inv_Property_Tab.Tests/Configuration/FieldsConfigurationStoreTests.cs`

- [ ] Добавить тесты нормализации порядка, клонирования настроек и отклонения некорректного поля.
- [ ] Добавить кнопку открытия настроек без жёстко заданного цвета или пути.
- [ ] Реализовать добавление, редактирование, удаление, видимость и перемещение полей.
- [ ] Реализовать редактор подписи, source, PropertySet, property name, editor и values.
- [ ] Редактировать копию конфигурации; отмена не меняет рабочую панель.
- [ ] Добавить импорт, экспорт и восстановление заводской конфигурации с подтверждением пользователя.
- [ ] После успешного сохранения перестроить панель и перечитать активный документ.
- [ ] Выполнить тесты и ручные сценарии всех типов редактора.
- [ ] Зафиксировать окно настройки отдельным коммитом.

### Task 8: Диагностика и обработка ошибок

**Files:**
- Create: `Inv_Property_Tab/Inv_Property_Tab/Diagnostics/AppLogger.cs`
- Modify: `Inv_Property_Tab/Inv_Property_Tab/StandardAddInServer.cs`
- Modify: `Inv_Property_Tab/Inv_Property_Tab/UI/PropBrowserPaneManager.cs`
- Modify: `Inv_Property_Tab/Inv_Property_Tab/UI/PropUI/PropBrowserControl.cs`
- Modify: `Inv_Property_Tab/Inv_Property_Tab/Services/SupplierStore.cs`

- [ ] Реализовать потокобезопасную запись лога с ограничением размера.
- [ ] Заменить пустые `catch` критических путей на логирование с контекстом операции.
- [ ] Не показывать повторяющиеся модальные окна для одной ошибки.
- [ ] Проверить недоступный каталог настроек, повреждённый JSON и ошибку COM-записи.
- [ ] Выполнить тесты и сборку; проверить отсутствие секретов и полных пользовательских данных в логе.
- [ ] Зафиксировать диагностику отдельным коммитом.

### Task 9: Inno Setup per-user

**Files:**
- Create: `installer/InvPropTab.iss`
- Create: `installer/README.md`
- Modify: `Inv_Property_Tab/Inv_Property_Tab/InvPropTab.addin`

- [ ] Проверить фактический механизм per-user регистрации .NET Framework COM/ActiveX на чистой Windows и зафиксировать используемые ключи.
- [ ] Настроить x64, `PrivilegesRequired=lowest` и установку в `{localappdata}\Programs\InvPropTab`.
- [ ] Копировать `.addin` в `{userappdata}\Autodesk\Inventor 2020\Addins`.
- [ ] Регистрировать только `AddInServer` и `PropBrowserActiveX` в `HKCU\Software\Classes` с корректным `CodeBase`.
- [ ] Проверять работающий Inventor и наличие Inventor 2020 до изменения системы.
- [ ] Реализовать upgrade по стабильному AppId и rollback при ошибке.
- [ ] При удалении сохранять `%APPDATA%\InvPropTab`; полное удаление предлагать отдельным подтверждаемым действием.
- [ ] Собрать установщик Inno Setup и проверить install/upgrade/uninstall в чистом профиле пользователя.
- [ ] Зафиксировать установщик отдельным коммитом.

### Task 10: Пользовательская и техническая документация

**Files:**
- Modify: `README.md`
- Create: `docs/installation.md`
- Create: `docs/building.md`
- Create: `docs/configuration.md`
- Create: `docs/architecture.md`
- Create: `CHANGELOG.md`

- [ ] Описать назначение, требования Inventor 2020 x64 и основные возможности.
- [ ] Описать установку, обновление, удаление и ручную диагностику.
- [ ] Описать сборку с настраиваемым путём к Autodesk interop без публикации DLL Autodesk.
- [ ] Документировать JSON-схему, типы редакторов, импорт, экспорт и восстановление.
- [ ] Документировать архитектуру, владение COM-ссылками и расположение данных.
- [ ] Добавить release notes первой подготовленной версии.
- [ ] Проверить все команды и пути на чистом окружении.
- [ ] Зафиксировать документацию отдельным коммитом.

### Task 11: Финальная проверка и GitHub Release

**Files:**
- Modify as needed only for verified defects.

- [ ] Запустить все unit tests и сохранить фактический результат.
- [ ] Выполнить чистую Release-сборку.
- [ ] Проверить Inventor: нет документа, деталь, сборка, read-only, переключение, закрытие, повторный запуск.
- [ ] Проверить отсутствие истории Description и работу полной настройки полей.
- [ ] Проверить установку, обновление и удаление без администратора.
- [ ] Проверить завершение Inventor без оставшегося процесса.
- [ ] Проверить Git на `.vs`, `bin`, `obj`, Autodesk DLL, логи, пользовательские JSON и секреты.
- [ ] Создать тег только после успешной проверки.
- [ ] Опубликовать GitHub Release только по отдельному явному запросу пользователя; приложить установщик, SHA-256 и release notes.
