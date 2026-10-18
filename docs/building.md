# Сборка

## Требования

- Visual Studio 2022 или совместимый MSBuild;
- .NET Framework 4.8 Developer Pack;
- Autodesk Inventor 2020 с `Autodesk.Inventor.Interop.dll`;
- Inno Setup 6 для установщика.

## Сборка и тесты

Из корня репозитория:

```text
dotnet restore "Inv_Property_Tab/Inv_Property_Tab.slnx"
dotnet test "Inv_Property_Tab/Inv_Property_Tab.Tests/Inv_Property_Tab.Tests.csproj" -c Release
dotnet build "Inv_Property_Tab/Inv_Property_Tab.slnx" -c Release --no-restore
```

По умолчанию interop ищется в:

```text
C:\Program Files\Autodesk\Inventor 2020\Bin\Public Assemblies
```

Другой путь задаётся MSBuild-свойством `Inventor2020PublicAssemblies`.

Обычная сборка не устанавливает add-in. Для локального развёртывания существует цель `DeployInventor2020Addin`.

## Установщик

Сначала выполните Release-сборку, затем:

```text
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" "installer\InvPropTab.iss"
```

Результат: `installer/output/InvPropTab-1.0.2-x64.exe`.

Каталог `installer/output` и Autodesk interop DLL не добавляются в Git.
