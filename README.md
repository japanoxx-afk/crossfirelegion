# crossfirelegion

Crossfire Legion Windows 런처. 한국어 UI, GitHub Releases 업데이트, 백업과 원본 복원이 가능한 모든 유닛 생산 패치를 제공합니다.

## 실행

1. [최신 릴리스](https://github.com/japanoxx-afk/crossfirelegion/releases/latest)의 `CrossfireLegion-Launcher-버전.zip`을 다운로드하고 쓰기 가능한 폴더에 압축을 풉니다.
2. `Launcher.exe`를 실행합니다. 게임 경로를 지정하고 게임을 종료한 상태에서 **모든 유닛 패치 적용**을 누릅니다.
3. **게임 실행**을 눌러 사용자 지정/오프라인 경기에서 생산 메뉴를 확인합니다. 설치 폴더 쓰기 권한이 없을 때만 런처를 관리자 권한으로 실행하세요.
4. **원본 복원**으로 적용 전 DLL을 되돌릴 수 있습니다.

기본 게임 경로: `C:\Program Files (x86)\Steam\steamapps\common\Crossfire Legion (Demo)`

Windows 10/11의 .NET Framework 4.8을 사용합니다. 게임 파일은 배포물에 포함하지 않습니다.

## 패치 범위와 검증 상태

`Crossfire_Legion_Data/Managed/Carbon.Simulation.dll`의 `Carbon.Simulation.RosterHelper.IsAbilityInRoster`가 항상 참을 반환하도록 수정합니다. `AbilitiesProcessor.AddAbility`가 덱에 선택하지 않은 생산·업그레이드 능력을 제외하는 분기를 통과시키는 방식입니다. 해당 진영의 건물에 원래 정의된 유닛 생산 능력이 대상이며, 다른 진영 유닛이나 미보유 DLC를 추가하지 않습니다. 자원·테크 요구 조건도 그대로 적용됩니다.

편성 화면의 9개 슬롯이나 저장된 덱 형식은 확장하지 않습니다. 게임 내 생산 필터를 해제해 같은 목적을 달성하는 방식입니다. AI에도 영향을 줄 수 있습니다. 캠페인의 스크립트나 별도 잠금 조건까지 해제하지 않습니다. 온라인 경기 호환성은 검증되지 않았으므로 오프라인/사용자 지정 테스트 용도로 사용하세요.

확인한 원본 DLL SHA-256:
`3196ffcd56e58372b4f90c541d22572491da0ba0956d1b0b4b8b4d4dd303731d`

자동 검증: 실제 DLL 복사본 패치, 적용 결과 IL 검사, 중복 적용 차단, 변경된 파일의 복원 차단, 바이트 단위 원본 복원. 실제 게임 경기에서 모든 유닛의 생산 및 메뉴 배치는 아직 검증하지 않았습니다.

백업은 원본 DLL 옆의 `.cfl-original`, 검증 기록은 `.cfl-patch.json`에 저장됩니다. 파일이 게임 업데이트로 바뀌면 기존 백업으로 덮어쓰지 않고 중단합니다. 이런 경우 Steam 파일 무결성 검사로 원본을 확보하고 백업을 별도로 보관한 뒤 다시 적용하세요.

## 런처 업데이트

**런처 업데이트** 버튼은 이 저장소의 최신 정식 GitHub Release를 확인합니다. 새 버전의 `update.zip`과 `update.sha256`을 내려받고 SHA-256, ZIP 구성, 실행 파일 버전을 확인한 뒤 별도 버전 폴더에 설치합니다. 성공하면 `current.txt`를 교체하고 새 런처를 실행합니다. 이전 버전은 남아 있으며 `previous.txt`에 직전 버전이 기록됩니다. 업데이트 실패 시 기존 실행 파일은 유지됩니다. 이 기능은 런처/패처를 업데이트하며, 게임에 패치를 다시 적용해야 할 때는 원본 복원 후 재적용하세요.

## 개발 및 배포

```powershell
./scripts/build.ps1
# 출력: dist/Launcher.exe, build/*.zip

# 원본 게임 DLL을 대상으로 복사본 테스트 (게임을 종료한 상태)
Start-Process ./dist/versions/0.1.0/Application.exe -ArgumentList '--self-test', '"C:\Program Files (x86)\Steam\steamapps\common\Crossfire Legion (Demo)"' -Wait
# 결과: dist/versions/0.1.0/test-result.txt
```

코드 변경을 커밋한 뒤 다음 명령으로 새 버전을 배포합니다.

```powershell
./scripts/release.ps1 -Version 0.1.1
```

이 명령은 `version.txt`를 올리고 커밋·태그·푸시합니다. GitHub Actions가 Windows에서 빌드하고 Release에 런처 ZIP과 업데이트 파일을 게시합니다. 매 변경 배포 시 버전을 증가시켜야 하며, Actions 성공 이후 사용자의 업데이트 버튼에 반영됩니다. GitHub 인증/저장소 쓰기 권한과 Actions 사용 가능 상태가 필요합니다.

빌드 의존성: Mono.Cecil 0.11.6 (MIT). NuGet 패키지의 고정 SHA-256을 검증합니다. 런처 소스와 배포 설정만 Git에 포함하며 게임 DLL, 세이브 및 개인 설정은 포함하지 않습니다.
