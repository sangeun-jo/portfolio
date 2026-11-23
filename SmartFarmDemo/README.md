# SmartFarmDemo

미니 스마트팜을 LLM 에이전트가 자연어·음성으로 제어하는 멀티 클라이언트 데모.
"조도 어때?", "조명 켜줘", "사진 찍어줘" 한 마디로 센서 조회·조명 제어·카메라 촬영이 실행된다.

## 플랫폼별 역할

| 클라이언트 | 역할 | 기술 |
|---|---|---|
| **폰 (브라우저)** | AI 음성 대화 | OpenAI Realtime API + WebRTC |
| **WPF 앱** | 센서 모니터링 + 수동 제어 | .NET 10, WPF/MVVM |
| **웹 (/twin)** | 디지털트윈 3D 시각화 | Three.js / WebGL |
| **MCP 클라이언트** (예정) | Claude/ChatGPT 앱에서 자연어·음성 제어 | MCP 서버 (C# SDK) |

모두 하나의 데몬(`localhost:5080`) 뒤에 붙는다 — 스마트팩토리의 "엣지 수집 → 플랫폼 → HMI/클라이언트" 구조를 그대로 따른다.

## 구성

```
[폰 브라우저 /voice]   [WPF 앱]              [웹 /twin]
   음성 대화            모니터링+제어            3D 디지털트윈
        │                  │                      │
        └──────────┬───────┴──────────────────────┘
                   ▼
        [SmartFarmDemo.Daemon]  (ASP.NET Core, :5080)
         ├─ /api/status /api/history   센서 텔레메트리
         ├─ /api/light  /api/photo     조명·카메라 제어
         ├─ /api/chat                  텍스트 에이전트 (function calling)
         ├─ /api/voice-token           Realtime 음성 세션 토큰
         ├─ /mcp                        MCP 서버 (예정, C# SDK)
         ├─ FarmSimulator              센서/조명/사진 시뮬레이션
         └─ AgentService               OpenAI tool-loop
                   │  (예정) MQTT/HTTP
                   ▼
        [라즈베리파이] = 엣지 노드  ← 별도 프로젝트/리포지토리로 개발 예정
         ├─ 센서: 온도·습도 (SHT31/DHT22), 조도 (BH1750)
         ├─ 액추에이터: 릴레이 → 성장용 조명
         └─ 카메라: Pi Camera Module
```

엣지 노드 코드는 이 리포지토리가 아닌 **별도 프로젝트**로 분리해 개발한다 (Python, MQTT/HTTP 브릿지).

하드웨어 도착 전까지 `FarmSimulator`가 센서값을 시뮬레이션하므로 소프트웨어만으로 전체 데모가 가능하다. 미니어처 하우징은 상용 미니 팜 모형 또는 레고 사용.

## 구현 상태

- [x] 데몬: ASP.NET Core API + 센서 시뮬레이터 (2초 틱)
- [x] 텍스트 에이전트: OpenAI function calling — `get_environment`, `get_history`, `set_light`, `capture_photo`
- [x] 음성 페이지: `/voice` — Realtime API ephemeral token + WebRTC (모바일 브라우저 대응)
- [x] 디지털트윈: `/twin` — Three.js 온실 씬 (조명 on/off에 성장등·조도 연동)
- [x] WPF 앱: 센서 카드 + 스파크라인 + 조명 토글 + 카메라 촬영
- [ ] **MCP 서버** (C# `ModelContextProtocol` SDK): farm tools를 Claude Desktop/ChatGPT에 노출 — 음성은 클라이언트 앱 내장 모드 활용, 원격 접속은 Tailscale+HTTP 전송 검토
- [ ] 라즈베리파이 엣지 노드 (MQTT 브릿지) — 하드웨어 도착 후, **별도 리포지토리**에서 개발
- [ ] 음성 세션에 farm tools 연동 (Realtime function calling)
- [ ] 텔레메트리 이력 DB (EF Core)
- [ ] 지원자 경험 데이터 기반 음성 Q&A 페르소나 (면접 데모용)

## 스마트팩토리와의 대응

| 이 프로젝트 | 스마트팩토리 |
|---|---|
| 라즈베리파이 엣지 노드 | 라인 엣지 게이트웨이/IPC |
| 센서 → API → 대시보드 | 설비 데이터 → IIoT 플랫폼 → MES/HMI |
| LLM tool loop + Realtime 음성 | AI 기반 설비 코파일럿/자연어 제어 |
| MCP 서버 (C#) | 설비/에이전트 툴을 표준 프로토콜로 개방 — AI 오케스트레이션 레이어 |
| WPF 모니터링 앱 | 생산/설비 모니터링 클라이언트 |
| 디지털트윈 3D 시각화 | 설비 디지털트윈/HMI |
| 조명 자동 제어 | SCADA 자동화 로직 |

## 실행

```bash
cd SmartFarmDemo/src/SmartFarmDemo.Daemon
dotnet run          # http://localhost:5080
                     # /twin  디지털트윈
                     # /bugsfarm/ 벅스팜 3D 데모 (구역 선택·비전 촬영 미리보기)
                     # /voice 음성 대화 (폰 브라우저 권장)
```

`.env` (SmartFarmDemo 루트, gitignore 처리됨):

```
OPENAI_API_KEY=sk-...
OPENAI_MODEL=gpt-4o-mini
OPENAI_REALTIME_MODEL=gpt-realtime-2.1
OPENAI_REALTIME_VOICE=marin
```

## 프로젝트 구조

| 프로젝트 | 역할 |
|---|---|
| `src/SmartFarmDemo.Daemon` | ASP.NET Core. API, 시뮬레이터, LLM 루프, 웹 페이지 호스팅 |
| `src/SmartFarmDemo.Client` | WPF 모니터링/제어 대시보드 (MVVM) |
| `src/SmartFarmDemo.Contracts` | 공유 DTO (`FarmStatus`, `EnvironmentReading`, 채팅 계약) |

## 스택

- .NET 10 / C#, ASP.NET Core minimal API
- WPF + CommunityToolkit.Mvvm
- OpenAI: Chat Completions function calling + Realtime API (WebRTC)
- MCP (Model Context Protocol) C# SDK — Claude/ChatGPT 앱 연동
- Three.js/WebGL 디지털트윈
- (예정) MQTT, EF Core, Raspberry Pi OS/GPIO
