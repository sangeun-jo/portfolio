# AgentCrew

LLM 에이전트가 미니 스마트팜을 자연어로 제어하는 Windows 데스크톱 프로젝트.
"물 줘", "지금 조도 어때?", "사진 찍어줘" 같은 채팅 한 줄로 센서 조회·조명 제어·카메라 촬영이 실행된다.

## 왜 만들었나

- 스마트팩토리의 실제 방향은 **자연어/AI 기반 설비 제어와 코파일럿** — 이 프로젝트는 그 미니어처다
- 채용 시장에서 가장 많이 요구되는 실무 기술: **LLM function calling + 실시간 데이터 파이프라인 + Windows 상주 소프트웨어**
- 기존 포트폴리오(WPF/MVVM, IoT 센서 연동, 영상 AI 검증)의 연장선 — 이번엔 LLM 에이전트 루프를 얹는다

## 구성

```
[WPF 채팅 UI]                ← Claude/ChatGPT 스타일 대화 + 실시간 대시보드
      │  채팅 메시지
      ▼
[AgentCrew.Daemon]           ← 에이전트 러닝타임 (Worker Service)
   ├─ LLM 루프 (function calling)
   ├─ Tool 레지스트리:
   │    get_environment()   → 온도·습도·조도 조회
   │    set_light(on/off)   → 조명 제어 (릴레이)
   │    capture_photo()     → 카메라 촬영
   │    get_status()        → 대시보드/이력 데이터
   ├─ 텔레메트리 수신·저장·브로드캐스트
   └─ 세션 카탈로그 (EF Core)
      │  MQTT / HTTP
      ▼
[라즈베리파이] = 엣지 노드
   ├─ 센서: 온도·습도 (SHT31/DHT22), 조도 (BH1750)
   ├─ 액추에이터: 릴레이 → 성장용 조명 on/off
   ├─ 카메라: Raspberry Pi Camera 또는 IP 카메라
   └─ 로컬 로직: 임계값 기반 자동 제어 (선택)
```

미니어처 하우징은 상용 미니 팜 모형 또는 레고로 제작 — 하드웨어는 센서/액추에이터만 직접 다룬다.

## 기능 (MVP)

- [ ] 채팅 UI: 자연어 명령 → LLM → tool 실행 → 결과를 채팅으로 응답
- [ ] 환경 대시보드: 온도·습도·조도 실시간 표시 (차트 + 현재값)
- [ ] 조명 제어: 채팅 명령 또는 대시보드 버튼으로 on/off
- [ ] 카메라 촬영: "사진 찍어줘" → 이미지가 채팅/대시보드에 표시
- [ ] 텔레메트리 이력: EF Core 저장, 그래프 조회
- [ ] 시뮬레이터 모드: 하드웨어 없이 센서값 시뮬레이션으로 데모 가능

## 스마트팩토리와의 대응

| 이 프로젝트 | 스마트팩토리 |
|---|---|
| 라즈베리파이 엣지 노드 | 라인 엣지 게이트웨이/IPC |
| 센서 → MQTT → 데몬 | 설비 데이터 → IIoT 플랫폼 → MES |
| LLM tool loop | AI 기반 설비 코파일럿/자연어 제어 |
| WPF 대시보드 | 생산/설비 모니터링 HMI |
| 임계값 자동 제어 | SCADA 자동화 로직 |
| 이력 DB | MES 생산 실적/추적 |

## 프로젝트 구조

| 프로젝트 | 역할 |
|---|---|
| `SmartFarmDemo/src/SmartFarmDemo.Daemon` | Worker Service. LLM 루프, tool 실행, 텔레메트리 수신 |
| `SmartFarmDemo/src/SmartFarmDemo.Client` | WPF 채팅 UI + 대시보드 |
| `SmartFarmDemo/src/SmartFarmDemo.Contracts` | 데몬↔클라이언트 공유 DTO/계약 |

## 스택

- .NET 10, C# / WPF (MVVM)
- LLM API function calling (Claude / OpenAI)
- MQTT (엣지 통신), EF Core (이력)
- Raspberry Pi OS, GPIO/릴레이, Pi Camera
