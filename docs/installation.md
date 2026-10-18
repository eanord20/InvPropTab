# Установка InvPropTab

## Требования

- Windows x64;
- Autodesk Inventor 2020 x64;
- .NET Framework 4.8;
- учётная запись Windows с доступом к собственным `%LOCALAPPDATA%` и `%APPDATA%`.

Права администратора не требуются.

## Установка

1. Закройте Autodesk Inventor.
2. Скачайте `InvPropTab-1.0.2-x64.exe` из раздела Releases.
3. Запустите установщик.
4. После завершения запустите Inventor 2020.
5. Откройте деталь или сборку.
6. В браузере документа должна появиться панель `InvPropTab`.

Установщик размещает:

- DLL: `%LOCALAPPDATA%\Programs\InvPropTab`;
- manifest: `%APPDATA%\Autodesk\Inventor 2020\Addins\InvPropTab.addin`;
- COM-регистрацию: `HKCU\Software\Classes`;
- пользовательские настройки: `%APPDATA%\InvPropTab`.

## Обновление

1. Закройте Inventor.
2. Запустите установщик новой версии поверх существующей.
3. Пользовательский `fields.json` и сохранённые значения остаются без изменений.

## Удаление

Удалите `InvPropTab` через «Установленные приложения» Windows. Настройки в `%APPDATA%\InvPropTab` сохраняются, чтобы повторная установка восстановила конфигурацию.

Для полного сброса после удаления вручную удалите `%APPDATA%\InvPropTab`.

## Если панель не появилась

1. Полностью закройте и снова запустите Inventor.
2. Проверьте «Сервис → Надстройки»: `InvPropTab` должен быть загружен.
3. Проверьте наличие `%APPDATA%\Autodesk\Inventor 2020\Addins\InvPropTab.addin`.
4. Проверьте журнал `%APPDATA%\InvPropTab\invproptab.log`.
5. Убедитесь, что установлен именно Inventor 2020 x64.

При обращении в Issues приложите версию Windows, версию Inventor, описание действий и журнал без конфиденциальных данных.
