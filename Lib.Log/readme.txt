Log 라이브러리(LibLog) — 한국어 가이드

1) 개요
- 목적: .NET 8 애플리케이션을 위한 고성능/안정형 로깅 라이브러리.
- 지원 싱크: 로컬 파일, MSSQL(DB), (선택) FTP 업로드.
- 핵심 특징:
  - 역압(Backpressure: Block/DropLatest/Sample1Percent)
  - Bounded Channel + 배치 처리 + 정상 종료 시 드레인
  - ObjectPool 로 GC 압력 최소화
  - Polly 기반 회로차단기/재시도
  - 파일 경합 방지(경로별 세마포어)
  - 시작 시 구성(Options) 검증 및 상세 오류
  - IOptionsMonitor 반영(샘플링 등 런타임 조정 가능)

2) 빠른 시작
A. 등록(Host Builder)
  var builder = Host.CreateApplicationBuilder(args);
  builder.UseLibLogFromConfig("LibLog"); // appsettings 의 LibLog 섹션 바인딩
  var app = builder.Build();
  await app.RunAsync();

B. DI(서비스 콜렉션)
  services.AddLibLog(); // 기본값으로 등록
  // 또는 델리게이트로 일부 오버라이드
  services.AddLibLog(opts => {
      opts.Local.Directory = "C:\\Logs";
  });

C. 최소 appsettings.json
  "LibLog": {
    "Local": { "Directory": "C:\\Logs" }
    // Database, Ftp 등은 필요할 때만 추가
  }

D. 사용(ILogger / ILogService)
- ILogger 그대로 사용하면 Provider가 파이프라인으로 전달합니다.
- 혹은 파사드 ILogService 사용:
  var svc = provider.GetRequiredService<Log.Abstractions.ILogService>();
  svc.Info("Application", "안녕하세요");            // 기본
  svc.InfoFile("Job", "파일로 기록");             // 파일 싱크로만
  svc.InfoDb("Job", "DB로 기록");                // DB 싱크로만

- 특정 싱크만 보내기(스코프 키 "Sinks")
  using var scope = logger.BeginScope(new[]{ new KeyValuePair<string, object?>("Sinks", "Database") });
  logger.LogInformation("이 메시지는 DB로만 갑니다");

- DeviceId 지정(스코프 키는 Routing.DeviceKeyField, 기본 "DeviceId")
  using var scope = logger.BeginScope(new[]{ new KeyValuePair<string, object?>("DeviceId", "JB_10.0.0.25") });
  logger.LogInformation("디바이스 바운드 로그");

3) 구성(설정) — appsettings.json 의 "LibLog" 섹션
- 섹션을 생략하면 LogOptions 의 기본값으로 동작합니다. 필요한 값만 작성하세요.

A. Formatting (기본값)
  Text=true, Json=false
  TimestampFormat="yyyy-MM-dd HH:mm:ss.fff"
  UseUtcTimestamp=false
  MaxMessageLength=65536

B. Routing (기본값)
  CategoryGroups = { "Default": ["*"] }
  DeviceKeyField="DeviceId"

C. Partitions (권장 기본값 반영)
  Default 그룹만 두고 시작하는 것을 권장합니다.
  - Shards: 4               // 병렬 소비자 수(코어/카테고리 분포에 맞춰 2~4)
  - QueueCapacity: 4000     // TPS × (Flush/1000) × 안전계수(3~5)
  - BatchSize: 128          // 로컬/DB 모두 효율적인 범위(64~256)
  - FlushIntervalMs: 150    // 100~300ms 권장(지연/IO 타협)
  - Backpressure: Block     // 핵심 로깅은 Block 권장(드롭 없음, 대기함)

D. Rooting (디렉터리 루트 결정)
  - DefaultRoot="log"; AllowScopeOverride=true
  - 기본 Rules(코드 내 기본):
    1) { Root:"Error",  MinLevel:"Error" }
    2) { Root:"Result", Categories:["result","job.result","ui.result"] }
    3) { Root:"Log",    Categories:["Application","Db","Jb","*"] }
  - 규칙은 “구체 → 범용” 순서를 반드시 지키세요. 첫 매칭이 승리합니다.

E. Local(파일)
  - Enabled=true, Directory=""
  - FileTemplate="{Root}/{yyyy}-{MM}-{dd}/{Category}{DeviceId?}.log"
    지원 토큰: {Root}, {RootByLevel}, {Project}, {Category}, {DeviceId}, {DeviceId?}, {yyyy}, {MM}, {dd}, {HH}, {mm}, {ss}
  - Rollover: Type=Size, MaxSizeMB=64 (환경에 따라 128 권장)
  - RetentionDays=14, MinFreeSpaceMB=512, FlushOnError=true
  - 주의: {Device?} 는 미지원 → {DeviceId?} 를 사용하세요.

F. Database(MSSQL)
  - Enabled, ConnectionStringName 또는 ConnectionString
  - TableName="IF_LOG", AutoCreateTable=true
  - BatchSize=500(권장), MaxConcurrency=1(로컬/단일 인스턴스 권장)
  - 스키마: Timestamp, Level, Category, DeviceId, Message, Exception, Scope(JSON)

G. Ftp(선택)
  - Enabled=false (기본)
  - Host/Port/Username/Password, RemoteDirectory
  - RemoteTemplate(기본 "{Project}/{yyyy}/{MM}/{dd}/{Category}{DeviceId?}.log")
  - AtomicUpload, ValidateCert, DeleteLocalAfterSuccess
  - MaxConcurrentUploads=2, UploadIntervalSec=5, MaxBatchFiles=200, MaxDelayMs=15000
  - FailureBackoffBaseSeconds=5, ExponentCap=8, MaxSeconds=600, JitterFactor=0.25

H. Sampling
  - Enabled=true
  - DebugSamplingPercentage=99  // Debug/Trace 로그 샘플링(드롭) 비율

I. CircuitBreakerOptions(Local/Db/Ftp 공통)
  - Failures=5, BreakSec=30

4) 주요 API
- DI 확장: Log.ServiceCollection.AddLibLog(this IServiceCollection, Action<LogOptions>?)
- Host 확장: Log.Hosting.LogHost.UseLibLogFromConfig / UseLibLog (섹션/델리게이트)
- 파사드: Log.Abstractions.ILogService
  - Trace/Debug/Info/Warn/Error/Critical(category, message, deviceId?, ex?)
  - InfoDb/WarnDb/ErrorDb(...), InfoFile/WarnFile/ErrorFile(...)
  - CreateLogger(category)

5) 파일별 기능 설명
- Log/ServiceCollection.cs
  - Options(ValidateOnStart), SinkFactory, ObjectPool<LogEntry>, PartitionManager(IHostedService), DatabaseInitializer, FtpUpload, LoggerProvider, ILogService 등록.
  - Provider 전용 필터 규칙 추가(내부 로깅 재귀 차단).

- Log/Hosting/LogHost.cs
  - Host 빌더 확장: 구성 섹션/델리게이트 바인딩, Provider 억제 카테고리 선택적 적용.

- Log/Hosting/ConfigureLogOptions.cs
  - Database.ConnectionStringName 이 있으면 ConnectionStrings 에서 실제 연결문자열 주입.

- Log/Hosting/DatabaseInitializer.cs
  - 앱 시작 시 테이블/인덱스 생성(멱등). 실패는 로깅 후 계속 진행.

- Log/Provider/LoggerProvider.cs
  - Microsoft.Extensions.Logging 파이프라인 → PartitionManager 로 전달.
  - Scope/State 수집, DeviceId 추출, ObjectPool<LogEntry> 사용.

- Log/Pipeline/PartitionManager.cs
  - Sink 생성, 파티션/샤드 관리. Enqueue → BoundedChannel → 배치 → Flush → Pool 반환.
  - 종료 시 채널 완료 알리고 잔여 배치 드레인 후 Sink Dispose.

- ShardWorker (PartitionManager 내부)
  - 샘플링(ILogSamplingStrategy) 적용, BatchSize/FlushInterval 로 배치 생성.
  - Backpressure: Block(대기), DropLatest(드롭), Sample1Percent(샘플링).

- Log/Pipeline/ILogSamplingStrategy.cs, RandomSamplingStrategy.cs
  - Debug/Trace 샘플링 비율 결정(Options.Sampling 반영).

- Log/Routing/LogRouter.cs, Log/Routing/Hash.cs
  - 카테고리 그룹 결정(글롭), FNV-1a 해시로 샤드 선택.

- Log/Internal/TemplateRenderer.cs
  - 파일/원격 템플릿 토큰 치환 및 sanitize. 미지원 토큰은 시작 시 Validator가 탐지.

- Log/Internal/RootResolver.cs
  - 규칙 첫 매칭 반환. AllowScopeOverride=true 이면 스코프 키 "Root"로 강제 지정 가능.

- Log/Internal/Masking.cs
  - password/pwd/token/apikey 형태의 key=value 마스킹.

- Log/Format/TextFormat.cs, Log/Format/JsonFormat.cs
  - 타임스탬프/레벨/카테고리/디바이스/메시지/예외 요약 생성, 길이 제한/마스킹 반영.

- Log/Sink/SinkFactory.cs
  - IServiceProvider에서 ILoggerFactory 지연 조회(순환 DI 방지). 활성화된 싱크만 생성.

- Log/Sink/LocalSink.cs
  - 경로별 세마포어로 롤오버+쓰기 직렬화, Append(UTF-8 BOM 없음), 오류 포함 라인 시 FlushOnError.

- Log/Sink/DbSink.cs
  - SqlBulkCopy로 대량 삽입. BatchSize/MaxConcurrency 로 안정성 확보.
  - Polly Retry + CircuitBreaker. 스키마/매핑 고정.

- Log/Sink/FtpUpload.cs
  - 마지막 수정 2분 경과 `.log` 수집, AsyncFtpClient 업로드, `.uploadfailed` 백오프 마커/지터.

- Log/Models/LogEntry.cs, Log/Models/RouteKey.cs
  - LogEntry DTO(Reset 포함), RouteKey(그룹/카테고리/디바이스).

6) 운영/튜닝 가이드
- 역압:
  - Block: 드롭 없이 처리(생산자 대기). 핵심 로깅 권장.
  - DropLatest: 과부하 시 드롭. 성능 우선/드롭 허용일 때.
  - Sample1Percent: Debug/Trace 샘플링.
- 파티션 사이징:
  - QueueCapacity ≈ TPS × (FlushIntervalMs/1000) × (3~5)
  - BatchSize 64~256(파일), 200~1000(DB) — 시작은 128/500 권장
  - FlushInterval 100~300ms — 150ms 권장
  - Shards 2~4 — 코어/범주 분포 기반
- DB:
  - MaxConcurrency 1~2(시작은 1). 인덱스/락/TempDB 경합을 최소화.
- 파일:
  - 템플릿으로 자연 분산 유도({Category}{DeviceId?}.log). 단일 파일 집중 방지.
- FTP:
  - 디렉터리 폭을 제한하고(날짜/카테고리 폴더), 실패 백오프 마커 확인.

7) 오류 처리/유실 특성
- 정상 운영: Block 역압 시 드롭 없음. 배치 Flush 후 풀 반환.
- 종료: StopAsync에서 채널 완료 → 잔여 배치 드레인 → Sink Dispose.
- 크래시/강제 종료: 메모리 내 대기/진행 중 배치는 유실될 수 있음(일반적인 메모리 큐 한계). 절대 무손실을 원하면 영속 큐가 필요(범위 외).

8) 자주 묻는 질문(FAQ)
- 파일명이 `Application_Device__log`처럼 이상해요?
  → {Device?}는 미지원 토큰입니다. {DeviceId?}를 사용하세요. Validator가 시작 시 알려줍니다.
- DB만 기록하고 싶어요?
  → 스코프 키 "Sinks" 값을 "Database"로 지정하세요(여러 개면 쉼표로 결합).
- DeviceId는 어디서 읽나요?
  → 스코프/State의 키(기본 "DeviceId"). Routing.DeviceKeyField로 변경 가능.

9) 한계 및 개선 여지
- 영속 큐 미제공(크래시 상황 무손실 불가). 필요 시 별도 큐/저널링 계층을 추가하세요.
- FTP 트리 전체 탐색은 비용 큼 — 폴더 구조를 날짜/카테고리별로 설계하세요.
- 트리밍/AOT: DbSink의 JsonSerializer.Serialize는 IL2026 경고. Source Generator(JsonSerializerContext) 적용 권장.

부록) 샘플 구성(권장치 기반)
  "LibLog": {
    "Partitions": {
      "Default": { "Shards": 4, "QueueCapacity": 4000, "BatchSize": 128, "FlushIntervalMs": 150, "Backpressure": "Block" }
    },
    "Local": { "Enabled": true, "Directory": "C:\\Logs" },
    "Database": { "Enabled": true, "ConnectionStringName": "Reporting", "BatchSize": 500, "MaxConcurrency": 1 }
  }
