using Haven.Domain.Enums;

using static Haven.Presentation.Api.Development.ProjectStackSeeder;

namespace Haven.Presentation.Api.Development;

/// <summary>
/// Seeds a "Debug Toolbox" project of tiny alpine/busybox containers whose behavior is driven by
/// feature flags (env vars), so states like crashes, exit codes, slow starts, failing health
/// checks and noisy logs can be reproduced on demand by flipping a flag and redeploying. A stack
/// is skipped if its project already exists.
/// </summary>
public class DebugStackSeeder : IDevelopmentSeeder
{
    public int Order => 15;

    public async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken) =>
        await ProjectStackSeeder.SeedStacksAsync(services, Stacks,
            services.GetRequiredService<ILoggerFactory>().CreateLogger<DebugStackSeeder>(), cancellationToken);

    private const string Image = "alpine:latest";

    private static string[] Script(string script) => ["sh", "-c", script];

    private static IReadOnlyList<ProjectStack> Stacks =>
    [
        new("Debug Toolbox", "debug", "Throwaway containers for exercising Haven features and states; behavior is controlled by feature flags",
            Env: NoEnv,
            Secrets: NoEnv,
            Environments:
            [
                new("Development", "dev", "Debug containers",
                    Env: NoEnv,
                    Secrets: NoEnv,
                    Services:
                    [
                        new("ticker", "ticker", Image)
                        {
                            Args = Script("while true; do echo \"[ticker] $(date -u +%FT%TZ) tick\"; sleep \"${TICK_INTERVAL_SECONDS:-5}\"; done"),
                            Flags = [new("Tick interval (s)", "TICK_INTERVAL_SECONDS", "5", FeatureFlagValueType.Number, "Seconds between log lines")]
                        },

                        new("crasher", "crasher", Image)
                        {
                            RestartPolicy = RestartPolicy.No,
                            Args = Script("echo \"[crasher] alive for ${LIFETIME_SECONDS:-30}s, then exiting with ${EXIT_CODE:-1}\"; sleep \"${LIFETIME_SECONDS:-30}\"; echo \"[crasher] exiting with ${EXIT_CODE:-1}\" >&2; exit \"${EXIT_CODE:-1}\""),
                            Flags =
                            [
                                new("Lifetime (s)", "LIFETIME_SECONDS", "30", FeatureFlagValueType.Number, "Seconds to stay alive before exiting"),
                                new("Exit code", "EXIT_CODE", "1", FeatureFlagValueType.Number, "Exit code to return (0 = clean exit)")
                            ]
                        },

                        new("crash loop", "crash-loop", Image)
                        {
                            RestartPolicy = RestartPolicy.OnFailure,
                            Args = Script("echo \"[crash-loop] starting, will exit in ${LIFETIME_SECONDS:-10}s\"; sleep \"${LIFETIME_SECONDS:-10}\"; exit \"${EXIT_CODE:-1}\""),
                            Flags =
                            [
                                new("Lifetime (s)", "LIFETIME_SECONDS", "10", FeatureFlagValueType.Number, "Seconds per run before exiting"),
                                new("Exit code", "EXIT_CODE", "1", FeatureFlagValueType.Number, "Exit code to return (non-zero keeps it restarting)")
                            ]
                        },

                        new("noisy logger", "noisy", Image)
                        {
                            Args = Script("""
                                i=0
                                while true; do
                                  i=$((i+1))
                                  echo "INFO  request $i handled"
                                  [ $((i % 3)) -eq 0 ] && echo "WARN  slow request $i" >&2
                                  [ $((i % 7)) -eq 0 ] && printf 'ERROR unhandled exception on request %s\n    at Handler.Run()\n    at Pipeline.Invoke()\n' "$i" >&2
                                  sleep "${LOG_INTERVAL_SECONDS:-1}"
                                done
                                """),
                            Flags = [new("Log interval (s)", "LOG_INTERVAL_SECONDS", "1", FeatureFlagValueType.Number, "Seconds between log bursts")]
                        },

                        new("flaky health", "flaky-health", Image)
                        {
                            Args = Script("""
                                sleep "${HEALTHY_AFTER_SECONDS:-10}"
                                touch /tmp/healthy
                                echo "[flaky-health] healthy"
                                if [ "${UNHEALTHY_AFTER_SECONDS:-0}" -gt 0 ]; then
                                  sleep "$UNHEALTHY_AFTER_SECONDS"
                                  rm -f /tmp/healthy
                                  echo "[flaky-health] now unhealthy"
                                fi
                                while true; do sleep 3600; done
                                """),
                            Flags =
                            [
                                new("Healthy after (s)", "HEALTHY_AFTER_SECONDS", "10", FeatureFlagValueType.Number, "Delay before the health marker appears"),
                                new("Unhealthy after (s)", "UNHEALTHY_AFTER_SECONDS", "0", FeatureFlagValueType.Number, "Seconds healthy before the marker is removed (0 = stay healthy)")
                            ],
                            HealthChecks =
                            [
                                new("Marker file", HealthCheckKind.Bash, """{"command":"test -f /tmp/healthy","expectedExitCode":0,"timeoutSeconds":5}""", "*/1 * * * *"),
                                new("Container running", HealthCheckKind.Container, "{}")
                            ]
                        },

                        // Slow-starting HTTP server with a probe-able endpoint. Verifies startup waits and HTTP checks.
                        new("slow http", "slow-http", "busybox:latest")
                        {
                            Ports = ["8101:80"],
                            Exposure = ExposureMode.Internal,
                            Args = Script("""
                                echo "[slow-http] starting up for ${STARTUP_DELAY_SECONDS:-15}s"
                                sleep "${STARTUP_DELAY_SECONDS:-15}"
                                mkdir -p /www && echo "ok" > /www/index.html
                                echo "[slow-http] listening on :80"
                                exec httpd -f -v -p 80 -h /www
                                """),
                            Flags = [new("Startup delay (s)", "STARTUP_DELAY_SECONDS", "15", FeatureFlagValueType.Number, "Seconds before the server starts listening")],
                            HealthChecks = [new("Root", HealthCheckKind.Http, """{"url":"http://localhost:8101","method":"GET","expectedStatusCodes":[200],"timeoutSeconds":5}""", "*/1 * * * *")]
                        },

                        // Burns CPU on N workers. Verifies resource/stat displays.
                        new("cpu burner", "cpu-burner", Image)
                        {
                            Args = Script("""
                                echo "[cpu-burner] burning ${CPU_WORKERS:-1} worker(s)"
                                n=0
                                while [ "$n" -lt "${CPU_WORKERS:-1}" ]; do
                                  (while :; do :; done) &
                                  n=$((n+1))
                                done
                                wait
                                """),
                            Flags = [new("CPU workers", "CPU_WORKERS", "1", FeatureFlagValueType.Number, "Number of busy loops (0 still keeps it running idle)")]
                        },

                        new("env dump", "env-dump", Image)
                        {
                            Env = new Dictionary<string, string> { ["PLAIN_VAR"] = "plain-value" },
                            Secrets = new Dictionary<string, string> { ["DEBUG_SECRET"] = "super-secret-value" },
                            Args = Script("echo \"[env-dump] environment:\"; env | sort; while true; do sleep 3600; done"),
                            Flags = [new("Greeting", "GREETING", "hello", FeatureFlagValueType.String, "Arbitrary string flag")]
                        },

                        new("volume writer", "volume-writer", Image)
                        {
                            Args = Script("while true; do date -u +%FT%TZ >> /data/heartbeat.log; sleep \"${WRITE_INTERVAL_SECONDS:-10}\"; done"),
                            Volumes = [new(VolumeType.Managed, "debug data", "/data", BackupEnabled: true) { Files = [("heartbeat.log", "# seeded\n")] }],
                            Flags = [new("Write interval (s)", "WRITE_INTERVAL_SECONDS", "10", FeatureFlagValueType.Number, "Seconds between writes")]
                        }
                    ])
            ])
    ];
}
