#define AppName "InvPropTab"
#define AppVersion "1.1.0"
#define AppPublisher "Nikolay Sukhomlin"
#define AppId "{{6F157B80-7E5D-4A0C-A2FD-C465741D2D82}"
#define AddInClsid "{{A199A936-8AAA-4DDC-8BED-A41DB48D894C}"
#define ControlClsid "{{A9F9E447-5A11-4A0C-9B8E-5A6C8B2F2D34}"
#define AddInClsidXml "{A199A936-8AAA-4DDC-8BED-A41DB48D894C}"

[Setup]
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=output
OutputBaseFilename=InvPropTab-{#AppVersion}-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
UninstallDisplayName={#AppName} {#AppVersion}
VersionInfoVersion={#AppVersion}.0
VersionInfoCompany={#AppPublisher}
VersionInfoDescription=Autodesk Inventor 2020 iProperties browser panel

[Files]
Source: "..\Inv_Property_Tab\Inv_Property_Tab\bin\Release\net48\Inv_Property_Tab.dll"; DestDir: "{app}"; Flags: ignoreversion

[Dirs]
Name: "{userappdata}\Autodesk\Inventor 2020\Addins"

[Registry]
Root: HKCU; Subkey: "Software\Classes\Inv_Property_Tab.AddIn.AddInServer"; ValueType: string; ValueName: ""; ValueData: "Inv_Property_Tab.AddIn.AddInServer"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Inv_Property_Tab.AddIn.AddInServer\CLSID"; ValueType: string; ValueName: ""; ValueData: "{#AddInClsid}"
Root: HKCU; Subkey: "Software\Classes\CLSID\{#AddInClsid}"; ValueType: string; ValueName: ""; ValueData: "Inv_Property_Tab.AddIn.AddInServer"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\CLSID\{#AddInClsid}\InprocServer32"; ValueType: string; ValueName: ""; ValueData: "mscoree.dll"
Root: HKCU; Subkey: "Software\Classes\CLSID\{#AddInClsid}\InprocServer32"; ValueType: string; ValueName: "ThreadingModel"; ValueData: "Both"
Root: HKCU; Subkey: "Software\Classes\CLSID\{#AddInClsid}\InprocServer32"; ValueType: string; ValueName: "Class"; ValueData: "Inv_Property_Tab.AddIn.AddInServer"
Root: HKCU; Subkey: "Software\Classes\CLSID\{#AddInClsid}\InprocServer32"; ValueType: string; ValueName: "Assembly"; ValueData: "Inv_Property_Tab, Version={#AppVersion}.0, Culture=neutral, PublicKeyToken=null"
Root: HKCU; Subkey: "Software\Classes\CLSID\{#AddInClsid}\InprocServer32"; ValueType: string; ValueName: "RuntimeVersion"; ValueData: "v4.0.30319"
Root: HKCU; Subkey: "Software\Classes\CLSID\{#AddInClsid}\InprocServer32"; ValueType: string; ValueName: "CodeBase"; ValueData: "file:///{app}\Inv_Property_Tab.dll"
Root: HKCU; Subkey: "Software\Classes\CLSID\{#AddInClsid}\ProgId"; ValueType: string; ValueName: ""; ValueData: "Inv_Property_Tab.AddIn.AddInServer"
Root: HKCU; Subkey: "Software\Classes\CLSID\{#AddInClsid}\Implemented Categories\{{62C8FE65-4EBB-45E7-B440-6E39B2CDBF29}"; ValueType: none

Root: HKCU; Subkey: "Software\Classes\InvPropTab.PropBrowser"; ValueType: string; ValueName: ""; ValueData: "Inv_Property_Tab.UI.PropUI.PropBrowserActiveX"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\InvPropTab.PropBrowser\CLSID"; ValueType: string; ValueName: ""; ValueData: "{#ControlClsid}"
Root: HKCU; Subkey: "Software\Classes\CLSID\{#ControlClsid}"; ValueType: string; ValueName: ""; ValueData: "Inv_Property_Tab.UI.PropUI.PropBrowserActiveX"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\CLSID\{#ControlClsid}\InprocServer32"; ValueType: string; ValueName: ""; ValueData: "mscoree.dll"
Root: HKCU; Subkey: "Software\Classes\CLSID\{#ControlClsid}\InprocServer32"; ValueType: string; ValueName: "ThreadingModel"; ValueData: "Both"
Root: HKCU; Subkey: "Software\Classes\CLSID\{#ControlClsid}\InprocServer32"; ValueType: string; ValueName: "Class"; ValueData: "Inv_Property_Tab.UI.PropUI.PropBrowserActiveX"
Root: HKCU; Subkey: "Software\Classes\CLSID\{#ControlClsid}\InprocServer32"; ValueType: string; ValueName: "Assembly"; ValueData: "Inv_Property_Tab, Version={#AppVersion}.0, Culture=neutral, PublicKeyToken=null"
Root: HKCU; Subkey: "Software\Classes\CLSID\{#ControlClsid}\InprocServer32"; ValueType: string; ValueName: "RuntimeVersion"; ValueData: "v4.0.30319"
Root: HKCU; Subkey: "Software\Classes\CLSID\{#ControlClsid}\InprocServer32"; ValueType: string; ValueName: "CodeBase"; ValueData: "file:///{app}\Inv_Property_Tab.dll"
Root: HKCU; Subkey: "Software\Classes\CLSID\{#ControlClsid}\ProgId"; ValueType: string; ValueName: ""; ValueData: "InvPropTab.PropBrowser"
Root: HKCU; Subkey: "Software\Classes\CLSID\{#ControlClsid}\Implemented Categories\{{62C8FE65-4EBB-45E7-B440-6E39B2CDBF29}"; ValueType: none
Root: HKCU; Subkey: "Software\Classes\CLSID\{#ControlClsid}\Control"; ValueType: none
Root: HKCU; Subkey: "Software\Classes\CLSID\{#ControlClsid}\MiscStatus"; ValueType: string; ValueName: ""; ValueData: "131457"

[UninstallDelete]
Type: files; Name: "{userappdata}\Autodesk\Inventor 2020\Addins\InvPropTab.addin"

[Code]
function XmlEscape(Value: String): String;
begin
  Result := Value;
  StringChangeEx(Result, '&', '&amp;', True);
end;

procedure WriteAddInManifest;
var
  Manifest: String;
  ManifestPath: String;
begin
  ManifestPath := ExpandConstant('{userappdata}\Autodesk\Inventor 2020\Addins\InvPropTab.addin');
  Manifest := '<?xml version="1.0" encoding="utf-8"?>' + #13#10 +
    '<Addin Type="Standard">' + #13#10 +
    '  <ClassId>{#AddInClsidXml}</ClassId>' + #13#10 +
    '  <ClientId>{#AddInClsidXml}</ClientId>' + #13#10 +
    '  <DisplayName>{#AppName}</DisplayName>' + #13#10 +
    '  <Description>Configurable iProperties browser panel</Description>' + #13#10 +
    '  <Assembly>' + XmlEscape(ExpandConstant('{app}\Inv_Property_Tab.dll')) + '</Assembly>' + #13#10 +
    '  <FullClassName>Inv_Property_Tab.AddIn.AddInServer</FullClassName>' + #13#10 +
    '  <LoadOnStartUp>1</LoadOnStartUp>' + #13#10 +
    '  <UserUnloadable>1</UserUnloadable>' + #13#10 +
    '</Addin>' + #13#10;
  ForceDirectories(ExtractFileDir(ManifestPath));
  SaveStringToFile(ManifestPath, Manifest, False);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    WriteAddInManifest;
end;
