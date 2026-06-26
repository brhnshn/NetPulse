[Version]
Class=IEXPRESS
SEDVersion=3

[Options]
PackagePurpose=InstallApp
ShowInstallProgramWindow=0
HideExtractAnimation=0
UseLongFileName=1
InsideCompressed=0
CAB_FixedSize=0
CAB_ResvCodeSigning=0
RebootMode=N
InstallPrompt=%InstallPrompt%
DisplayLicense=%DisplayLicense%
FinishMessage=%FinishMessage%
TargetName=%TargetName%
FriendlyName=%FriendlyName%
AppLaunched=%AppLaunched%
PostInstallCmd=%PostInstallCmd%
AdminQuietInstCmd=%AdminQuietInstCmd%
UserQuietInstCmd=%UserQuietInstCmd%
SourceFiles=SourceFiles

[Strings]
InstallPrompt=NetPulse kurulumu baslatilacak.
DisplayLicense=
FinishMessage=NetPulse kurulumu baslatildi. Yonetici izni istenirse onaylayin.
TargetName=SetupOutput\NetPulseSetup.exe
FriendlyName=NetPulse Kurulum Sihirbazi
AppLaunched=powershell.exe -NoProfile -ExecutionPolicy Bypass -File InstallNetPulse.ps1
PostInstallCmd=<None>
AdminQuietInstCmd=
UserQuietInstCmd=
FILE0=InstallNetPulse.ps1
FILE1=AppBundle.zip

[SourceFiles]
SourceFiles0=installer\iexpress_payload\

[SourceFiles0]
%FILE0%=
%FILE1%=
