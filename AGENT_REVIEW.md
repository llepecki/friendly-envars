# FriendlyEnvars 2.0 Remediation Specification

- **Specification status:** Approved for implementation
- **Specification date:** 2026-09-03
- **Review baseline revision:** `8680adbb771de78dbec6ced90b8e33b63aabb960` plus the staged working-tree changes present during review
- **Compatibility baseline:** Published NuGet package `FriendlyEnvars` 1.1.0
- **Target release:** `FriendlyEnvars` 2.0.0
- **Audience:** An autonomous implementation agent responsible for completing every repository-controlled requirement in one change set

## Mission and completion rule

Implement every requirement mapped to `REV-H01` through `REV-L06`. All 27 findings are mandatory. Priority labels determine implementation order, not optionality.

The work has two completion states:

1. **Repository complete:** every repository-controlled acceptance criterion and automated quality gate in this specification passes.
2. **Release authorized:** repository completion is achieved and the Captain completes the external GitHub/NuGet operator gates. An implementation agent without those external permissions must report `REPOSITORY COMPLETE — RELEASE BLOCKED BY OPERATOR GATES`; it must not publish or claim full release readiness.

Do not reinterpret a requirement, select a different API contract, defer a finding, or substitute documentation for a required code change. If a requirement proves technically impossible, stop and report the exact evidence instead of silently choosing another behavior.

## Corrected baseline facts

- The empty-string behavior is already part of the 1.1.0 source/release baseline. It is a historical compatibility concern, not a new 2.0 change.
- Sealing `EnvarsException` on the reviewed branch is not part of published 1.1.0 and is an intentional 2.0 break.
- Removing `BlockOptionsSnapshot()` and `BlockOptionsMonitor()` is an additional intentional 2.0 break.
- The library remains targeted at `net8.0`. Tests and samples must exercise both the minimum library target (`net8.0`) and current LTS (`net10.0`). `net9.0` support is removed from the sample.
- On the specification date, .NET runtimes 8.0.30 and 10.0.11, and SDKs 8.0.424 and 10.0.400, are the current supported patches. Commit `global.json` selecting SDK 10.0.400 with `rollForward: disable` and `allowPrerelease: false`; CI installs exactly SDKs 8.0.424 and 10.0.400 so both runtimes are present. Project package versions and lock files are exact.

## Locked implementation decisions

These decisions are authoritative for every finding below.

| Topic | Required decision |
|---|---|
| Release | Set `VersionPrefix` to `2.0.0`; validate the package against published 1.1.0 and check in justified suppressions only for intentional 2.0 breaks. |
| Environment lifecycle | Each `BindEnvars` call discovers/validates properties and captures each raw environment string synchronously during that call. No later options resolution may read the process environment. |
| Options instances | Captured raw strings are converted and assigned into each new options instance. Converted custom objects are not cached or shared between options instances. |
| Binding plan | Build one immutable plan per `(options type, options name)` registration. The plan is captured by that registration and is not stored in a process-wide static cache. |
| Duplicate registration | A second FriendlyEnvars registration for the same `(T, options name)` throws `InvalidOperationException` synchronously. Different names and standard Microsoft Options registrations remain allowed. |
| Properties | Accept only public instance, non-indexed properties with a public instance `set` or `init` accessor. Validate them during `BindEnvars`, even when the variable is absent. Annotate `T` for trimmer preservation of public properties. |
| Blocking APIs | Remove `BlockOptionsSnapshot()`, `BlockOptionsMonitor()`, their settings state, DI replacements, tests, and documentation. Do not add a replacement blocking API. |
| Precedence | Preserve Microsoft Options registration order. Configuration registered before `BindEnvars` can be overwritten by captured environment values; configuration registered afterward can overwrite FriendlyEnvars values. |
| Custom binders | The supplied binder instance is trusted, shared, deterministic, secret-safe, and thread-safe. Capture the reference after configuration. Do not add strict mode or a binder-factory API in 2.0. |
| Culture | Clone the selected `CultureInfo`, wrap the clone with `CultureInfo.ReadOnly`, and capture it after configuration. Later caller mutation must not alter binding. |
| Property assignment | Retain `PropertyInfo.SetValue` for 2.0. Precompute validated metadata; do not introduce expression compilation or source generation in this remediation. |
| Flags enums | Apply the exact flags grammar under `REV-M02`; do not delegate syntax decisions to `Enum.Parse`. Allow declared names and safe non-negative decimal bit patterns only. |
| Environment names | Reject null, empty, whitespace-only, `=`-containing, and Unicode-control-containing names (`char.IsControl`). Preserve all other names, including Unicode and ordinary embedded spaces. |
| Framework matrix | Library: `net8.0`. Tests: `net8.0;net10.0`. Sample: `net8.0;net10.0`. |
| Dependency policy | Use only nuget.org, commit per-project lock files, restore in locked mode, audit all transitive packages, and fail on Moderate/High/Critical advisories. |
| Performance proof | Deterministic operation-count tests are release-blocking. BenchmarkDotNet results are recorded and must show no regression beyond the thresholds in `REV-M08`. |
| Publication | Use SHA-pinned Actions, isolated jobs, a protected `nuget-production` environment, and NuGet trusted publishing/OIDC. Long-lived `NUGET_API_KEY` publication is removed. |

## Target binding architecture

### Registration

Every public `BindEnvars<T>` entry point must declare `[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]` on generic parameter `T`. `BindEnvars<T>` must then perform the following steps synchronously and in this order:

1. Reject a null `OptionsBuilder<T>`.
2. Create settings, invoke the caller's configuration delegate, and validate/capture the binder and read-only culture clone.
3. Reject an existing FriendlyEnvars marker for `(typeof(T), optionsBuilder.Name)`, but do not mutate the service collection yet.
4. Reflect properties with `BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static` and inspect `CustomAttributeData` to select `[Envar]` properties without invoking the attribute constructor. This deliberately includes public static properties so they can be rejected; non-public properties are outside the binding surface and are ignored. Decode the single string constructor argument and validate every selected property shape and environment name before any environment read.
5. Build immutable per-property metadata containing the property, environment name, declared property type, nullable underlying type, enum/flags metadata, and setter metadata.
6. Through an internal `IEnvironmentVariableReader.GetEnvironmentVariable(string)` seam, call the production `Environment.GetEnvironmentVariable` adapter once for each selected property and store the resulting nullable raw string in the immutable entry. The public extension calls an internal core overload with the production singleton; tests call that same core overload with an instrumented reader. The same internal overload accepts an `IBindingPlanObserver`; the centralized plan builder calls `PlanBuildStarted()` once and `MetadataInspected(PropertyInfo)` once per selected property, while the production path uses a no-op singleton. Do not expose either seam publicly or retain the observer in the registered configurator.
7. Only after every validation/read succeeds, add one internal marker and one named options configurator that closes over the completed plan, binder reference, and read-only culture.

Do not add the current no-op configurator. Do not retain `Type`, `PropertyInfo`, or attribute references in a static collection.

### Options creation

For every options instance created through `IOptions<T>`, `IOptionsSnapshot<T>`, `IOptionsMonitor<T>`, or `IOptionsFactory<T>`:

1. Iterate the captured plan in its registration-time discovery order; cross-property ordering is an implementation detail and not a public behavioral contract.
2. Skip entries whose captured raw value is null.
3. Convert the captured raw string with the captured binder and culture.
4. Assign the result through the validated property.
5. Never read `Environment.GetEnvironmentVariable`.

Changes to the process environment after `BindEnvars` returns must affect none of those options abstractions. The custom binder is called exactly once for each present plan entry reached while creating each options instance; conversion/assignment is fail-fast and later entries are not attempted after a failure. Deterministic/thread-safe binder behavior is the caller's contract.

### Safe exception contract

Add a public enum with exactly these members:

```csharp
public enum EnvarFailureKind
{
    InvalidProperty,
    EnvironmentRead,
    Conversion,
    Assignment
}
```

Keep `EnvarsException` sealed. Retain its existing public constructors for source compatibility, but library-generated binding failures must use a dedicated internal constructor and populate these public get-only properties:

```csharp
public EnvarFailureKind? FailureKind { get; }
public string? EnvironmentVariableName { get; }
public Type? OptionsType { get; }
public string? OptionsName { get; }
public string? PropertyName { get; }
public Type? TargetType { get; }
public string? CultureName { get; }
public Type? BinderType { get; }
public string? CauseType { get; }
```

The legacy public constructors leave every structured property null. Library-generated failures must match one and only one row below.

For all failure kinds, `TargetType` is exactly the decorated property's declared `PropertyInfo.PropertyType`, including `Nullable<T>` rather than its underlying type. `OptionsName` is the exact raw name and uses `Options.DefaultName` for default options. In message text only, `displayName` is `<default>` for `Options.DefaultName`; otherwise escape backslash, apostrophe, and every Unicode control character as `\\`, `\'`, or `\uXXXX` respectively.

Populate structured metadata exactly as follows (`cause` means the caught exception's full type name, unwrapping `TargetInvocationException.InnerException` when present):

| `FailureKind` | `EnvironmentVariableName` | `OptionsType` | `OptionsName` | `PropertyName` | `TargetType` | `CultureName` | `BinderType` | `CauseType` |
|---|---|---|---|---|---|---|---|---|
| `InvalidProperty` — unsupported shape | validated attribute name | exact `typeof(T)` | exact registration name | exact property name | declared property type | null | null | null |
| `InvalidProperty` — invalid attribute name | null | exact `typeof(T)` | exact registration name | exact property name | declared property type | null | null | null |
| `InvalidProperty` — type discovery failure | null | exact `typeof(T)` | exact registration name | null | null | null | null | cause |
| `InvalidProperty` — per-property metadata failure | null | exact `typeof(T)` | exact registration name | exact property name | declared property type | null | null | cause |
| `EnvironmentRead` | validated attribute name | exact `typeof(T)` | exact registration name | exact property name | declared property type | null | null | cause |
| `Conversion` | validated attribute name | exact `typeof(T)` | exact registration name | exact property name | declared property type | captured read-only culture's `Name` | captured binder's runtime type | cause |
| `Assignment` | validated attribute name | exact `typeof(T)` | exact registration name | exact property name | declared property type | null | null | cause |

Library-generated messages must use only the safe metadata above and one of these forms:

- Invalid property with a valid name: `Property '{OptionsType.FullName}.{PropertyName}' mapped to environment variable '{EnvironmentVariableName}' is not a supported bind target.`
- Invalid property with an invalid name: `Property '{OptionsType.FullName}.{PropertyName}' has an invalid environment-variable name.`
- Type discovery: `Failed to inspect environment-variable bindings for options type '{OptionsType.FullName}' (options name '{displayName}').`
- Per-property metadata: `Failed to inspect environment-variable binding metadata for property '{OptionsType.FullName}.{PropertyName}' (options name '{displayName}').`
- Environment read: `Failed to read environment variable '{EnvironmentVariableName}' for option '{OptionsType.FullName}.{PropertyName}' (options name '{displayName}').`
- Conversion: `Failed to convert environment variable '{EnvironmentVariableName}' to '{TargetType.FullName}' for option '{OptionsType.FullName}.{PropertyName}' (options name '{displayName}').`
- Assignment: `Failed to assign environment variable '{EnvironmentVariableName}' to option '{OptionsType.FullName}.{PropertyName}' (options name '{displayName}').`

Do not include the raw value, invalid attribute-name text, exception message, stack trace text, or any value-derived substring. Library-generated `EnvarsException` instances must have `InnerException == null`. Catch and sanitize exceptions thrown by custom binders even when the binder throws `EnvarsException`. Propagate `OperationCanceledException` unchanged.

## Mandatory implementation sequence

1. Repair the solution/package/test baseline (`REV-H04` through `REV-H07`) so subsequent gates can execute.
2. Set the 2.0 compatibility policy and API baseline (`REV-H08`, `REV-H09`).
3. Implement the registration plan, lifecycle, property/name validation, duplicate policy, flags policy, culture capture, and safe exception model (`REV-H01`, `REV-H03`, `REV-M01` through `REV-M04`, `REV-M07` through `REV-M10`, `REV-L01`, `REV-L02`, `REV-L04`).
4. Complete tests for every contract before changing documentation.
5. Repair README/XML/sample/named-options/package-debugging documentation (`REV-M11`, `REV-M12`, `REV-L03`, `REV-L05`, `REV-L06`).
6. Lock and audit dependencies, then split and harden CI/publication (`REV-H02`, `REV-M05`, `REV-M06`).
7. Add deterministic performance tests and benchmarks, run every repository quality gate, and record results.
8. Stop at the operator gate unless the Captain explicitly supplies the required external authority.

## Finding-to-file ownership map

The paths below are mandatory primary implementation/test surfaces. Helper types for the binding lifecycle go in `src/FriendlyEnvars/BindingPlan.cs`, `src/FriendlyEnvars/EnvironmentVariableReader.cs`, and `src/FriendlyEnvars/FriendlyEnvarsRegistrationMarker.cs`; do not hide those contracts in the sample or test projects.

| Finding | Required primary paths |
|---|---|
| `REV-H01` | `src/FriendlyEnvars/EnvarsException.cs`; new `src/FriendlyEnvars/EnvarFailureKind.cs`; `src/FriendlyEnvars/OptionsBuilderExtensions.cs`; `tests/FriendlyEnvars.Tests/ExceptionSafetyTests.cs` |
| `REV-H02` | `.github/workflows/ci.yml`; `.config/dotnet-tools.json`; `eng/validate-workflows.sh`; `eng/generate-sbom.sh`; `eng/verify-sbom.sh`; `eng/FriendlyEnvars.RepositoryVerifier/` |
| `REV-H03` | `src/FriendlyEnvars/OptionsBuilderExtensions.cs`; `src/FriendlyEnvars/BindingPlan.cs`; `src/FriendlyEnvars/EnvironmentVariableReader.cs`; `tests/FriendlyEnvars.Tests/OptionsLifecycleTests.cs` |
| `REV-H04` | `FriendlyEnvars.slnx` |
| `REV-H05` | `src/FriendlyEnvars/FriendlyEnvars.csproj`; `resources/icon-v2.png`; `eng/verify-package.sh` |
| `REV-H06` | `global.json`; `tests/FriendlyEnvars.Tests/FriendlyEnvars.Tests.csproj`; `sample/FriendlyEnvars.Sample/FriendlyEnvars.Sample.csproj`; `.github/workflows/ci.yml`; `tests/FriendlyEnvars.Tests/PortabilityContractTests.cs` |
| `REV-H07` | `sample/FriendlyEnvars.Sample/Program.cs`; `sample/FriendlyEnvars.Sample/FriendlyEnvars.Sample.csproj`; `eng/run-sample.sh`; `FriendlyEnvars.slnx` |
| `REV-H08` | `src/FriendlyEnvars/FriendlyEnvars.csproj`; `src/FriendlyEnvars/CompatibilitySuppressions.xml`; `README.md`; `tests/FriendlyEnvars.Tests/BehavioralBreakContractTests.cs`; `eng/verify-package.sh` |
| `REV-H09` | `src/FriendlyEnvars/OptionsBuilderExtensions.cs`; `src/FriendlyEnvars/EnvarSettings.cs`; `tests/FriendlyEnvars.Tests/OptionsResolutionTests.cs`; `eng/verify-api-removals.sh` |
| `REV-M01` | `src/FriendlyEnvars/BindingPlan.cs`; `src/FriendlyEnvars/OptionsBuilderExtensions.cs`; `tests/FriendlyEnvars.Tests/PropertyAccessibilityTests.cs` |
| `REV-M02` | `src/FriendlyEnvars/DefaultEnvarPropertyBinder.cs`; `tests/FriendlyEnvars.Tests/DefaultEnvarPropertyBinderTests.cs` |
| `REV-M03` | `src/FriendlyEnvars/EnvarSettings.cs`; `src/FriendlyEnvars/BindingPlan.cs`; `src/FriendlyEnvars/OptionsBuilderExtensions.cs`; `tests/FriendlyEnvars.Tests/EnvarSettingsTests.cs`; `tests/FriendlyEnvars.Tests/OptionsLifecycleTests.cs` |
| `REV-M04` | `src/FriendlyEnvars/IEnvarPropertyBinder.cs`; `src/FriendlyEnvars/EnvarSettings.cs`; `src/FriendlyEnvars/DefaultEnvarPropertyBinder.cs`; `README.md`; `tests/FriendlyEnvars.Tests/BindingConcurrencyTests.cs`; `tests/FriendlyEnvars.Tests/ExceptionSafetyTests.cs` |
| `REV-M05` | `NuGet.config`; `Directory.Build.props`; every `packages.lock.json`; `.github/dependabot.yml`; `.github/workflows/ci.yml`; `eng/audit-dependencies.sh` |
| `REV-M06` | `sample/FriendlyEnvars.Sample/FriendlyEnvars.Sample.csproj`; `sample/FriendlyEnvars.Sample/packages.lock.json`; `eng/audit-dependencies.sh` |
| `REV-M07` | `src/FriendlyEnvars/BindingPlan.cs`; `src/FriendlyEnvars/OptionsBuilderExtensions.cs`; `tests/FriendlyEnvars.Tests/AssemblyUnloadTests.cs` |
| `REV-M08` | `src/FriendlyEnvars/BindingPlan.cs`; `src/FriendlyEnvars/DefaultEnvarPropertyBinder.cs`; `tests/FriendlyEnvars.TrimSmoke/`; `benchmarks/FriendlyEnvars.Benchmarks/`; `eng/trim-smoke.sh`; `eng/compare-benchmarks.sh`; `eng/FriendlyEnvars.RepositoryVerifier/` |
| `REV-M09` | `src/FriendlyEnvars/BindingPlan.cs`; `src/FriendlyEnvars/OptionsBuilderExtensions.cs`; `tests/FriendlyEnvars.Tests/BindingConcurrencyTests.cs`; `src/FriendlyEnvars/IEnvarPropertyBinder.cs`; `README.md` |
| `REV-M10` | `src/FriendlyEnvars/EnvarAttribute.cs`; `src/FriendlyEnvars/BindingPlan.cs`; `src/FriendlyEnvars/OptionsBuilderExtensions.cs`; `tests/FriendlyEnvars.Tests/EnvironmentNameTests.cs` |
| `REV-M11` | all `src/FriendlyEnvars/*.cs` public XML comments; `src/FriendlyEnvars/FriendlyEnvars.csproj`; `eng/verify-docs.sh`; `eng/FriendlyEnvars.RepositoryVerifier/` |
| `REV-M12` | `README.md`; `eng/smoke-consumer.sh` |
| `REV-L01` | `src/FriendlyEnvars/OptionsBuilderExtensions.cs`; `src/FriendlyEnvars/EnvarsException.cs`; `tests/FriendlyEnvars.Tests/ExceptionSafetyTests.cs`; `tests/FriendlyEnvars.Tests/CancellationTests.cs` |
| `REV-L02` | `src/FriendlyEnvars/OptionsBuilderExtensions.cs`; `src/FriendlyEnvars/FriendlyEnvarsRegistrationMarker.cs`; `tests/FriendlyEnvars.Tests/RegistrationTests.cs` |
| `REV-L03` | `.gitignore`; `.gitleaks.toml`; `sample/FriendlyEnvars.Sample/Program.cs`; `README.md`; `.github/workflows/ci.yml`; `eng/secret-scan.sh` |
| `REV-L04` | `src/FriendlyEnvars/OptionsBuilderExtensions.cs`; `README.md`; `tests/FriendlyEnvars.Tests/PrecedenceTests.cs` |
| `REV-L05` | `README.md`; `sample/FriendlyEnvars.Sample/Program.cs`; `tests/FriendlyEnvars.Tests/NamedOptionsTests.cs` |
| `REV-L06` | `src/FriendlyEnvars/FriendlyEnvars.csproj`; `.config/dotnet-tools.json`; `eng/verify-sourcelink.sh`; `eng/verify-reproducible-package.sh`; `eng/verify-published-package.sh`; `eng/FriendlyEnvars.RepositoryVerifier/`; `.github/workflows/ci.yml` |

## Per-finding implementation contracts

### REV-H01 — Remove environment-secret disclosure

**Required change**

Implement the safe exception contract above for conversion, environment-read, metadata, and assignment failures. Sanitize exceptions from the default binder, custom binders, setters, and nested `EnvarsException` instances. Remove all tests and sample output that expect or print raw values.

**Acceptance criteria**

- Tests use unique sentinels containing a fake token, newline, ANSI escape sequence, and at least 4 KiB of text.
- For default conversion, custom binder, custom `EnvarsException`, and throwing setter cases, the sentinel and every 8-character sentinel substring are absent from `Message`, `ToString()`, `InnerException`, and structured properties.
- `InnerException` is null for every library-generated `EnvarsException`.
- All safe structured properties and exact `FailureKind` values are asserted.
- Environment-variable name, options type/name, property, target type, culture, binder, and sanitized cause type remain diagnosable.

### REV-H02 — Make package publication supply-chain-safe

**Required change**

Keep `.github/workflows/ci.yml`, set top-level `permissions: {}`, and split it into jobs with these exact trust boundaries:

- `validate`: `permissions: { contents: read }`; no secrets; checkout with `persist-credentials: false` and `fetch-depth: 0`; restore, format verification, build, test matrix, validation pack, sample/consumer/trim smoke, audit, and `eng/secret-scan.sh`.
- `package`: runs only for a push to `refs/heads/master`, depends on `validate`, and has only `contents: read`, `id-token: write`, `attestations: write`, and `artifact-metadata: write`. It performs a clean checkout of `${{ github.sha }}`, locked restore/build/pack on Ubuntu, and treats that pack as the sole publication build. It generates `FriendlyEnvars.2.0.0.nupkg`, `FriendlyEnvars.2.0.0.snupkg`, `sbom.spdx.json`, and `SHA256SUMS`, then uploads exactly those four files as immutable artifact `friendly-envars-2.0.0-${{ github.sha }}`.
- `report-tests`: separate unprivileged job with only `contents: read` and `checks: write`; independently checks out the candidate, performs locked restore, reruns both unit-test targets to its own TRX directory, and uses the allowlisted pinned reporter. It neither downloads nor uploads a package artifact and is not a dependency of `validate`, `package`, or `publish`.
- `publish`: runs only for a push to `refs/heads/master`, depends on `package`, and uses `environment: nuget-production`; permissions are only `contents: read`, `attestations: read`, and `id-token: write`. It downloads the exact SHA-named artifact, verifies `SHA256SUMS` and the provenance plus SBOM attestation for each package, obtains a short-lived key through `NuGet/login`, and pushes exactly `FriendlyEnvars.2.0.0.nupkg`.

Commit `Microsoft.Sbom.DotNetTool` 4.1.5 to the local tool manifest. `eng/generate-sbom.sh artifacts/release` must run `dotnet tool run sbom-tool generate -b artifacts/release -bc . -pn FriendlyEnvars -pv 2.0.0 -ps "Lukasz Lepecki" -nsb https://github.com/llepecki/friendly-envars/sbom -m <temporary-directory>`, run `dotnet tool run sbom-tool validate -b artifacts/release -m <temporary-directory>/_manifest -mi SPDX:2.2 -o <temporary-directory>/validation.json`, and only then copy `<temporary-directory>/_manifest/spdx_2.2/manifest.spdx.json` to `artifacts/release/sbom.spdx.json`. `eng/verify-sbom.sh` performs the repository-specific semantic checks below. Invoke `actions/attest@a1948c3f048ba23858d222213b7c278aabede763 # v4.1.1` exactly four times: once for each NuGet package with that single file as `subject-path` and no predicate input to generate its SLSA build provenance, then once for each package with that same single-file `subject-path` plus `sbom-path: artifacts/release/sbom.spdx.json` to generate its SBOM attestation. Do not create a multi-subject attestation. `SHA256SUMS` must contain SHA-256 entries for both packages and the SBOM, sorted by filename.

Every remote `uses:` reference must exactly match this allowlist and include the shown release comment; no additional remote action is permitted:

| Action | Required immutable reference |
|---|---|
| Checkout | `actions/checkout@de0fac2e4500dabe0009e67214ff5f5447ce83dd # v6.0.2` |
| Setup .NET | `actions/setup-dotnet@c2fa09f4bde5ebb9d1777cf28262a3eb3db3ced7 # v5.2.0` |
| Upload artifact | `actions/upload-artifact@043fb46d1a93c77aae656e7c1c64a875d1fc6a0a # v7.0.1` |
| Download artifact | `actions/download-artifact@3e5f45b2cfb9172054b4087a40e8e0b5a5461e7c # v8.0.1` |
| Artifact attestation | `actions/attest@a1948c3f048ba23858d222213b7c278aabede763 # v4.1.1` |
| Test reporter | `dorny/test-reporter@7b7927aa7da8b82e81e755810cb51f39941a2cc7 # v2.2.0` |
| NuGet OIDC login | `NuGet/login@d22cc5f58ff5b88bf9bd452535b4335137e24544 # v1.1.0` |

Remove long-lived `NUGET_API_KEY` use.

**Acceptance criteria**

- `eng/validate-workflows.sh` fails if any remote workflow `uses:` value is not one of the seven exact references above, if a release comment is absent/mismatched, or if job permissions/conditions/dependency edges differ from this contract.
- No third-party action executes in `package` or `publish`; only SHA-pinned `actions/*` and `NuGet/login` are allowed there.
- `publish` is unreachable for pull requests and non-master refs.
- `eng/verify-sbom.sh` asserts valid SPDX 2.2 JSON, package identity/version, both NuGet artifact filenames, and a relationship from the FriendlyEnvars package to each declared dependency.
- For each `.nupkg` and `.snupkg`, `publish` runs both commands below, substituting the downloaded artifact path for `<artifact>` and the exact workflow commit for `<final-sha>`. Each command must succeed and emit JSON to a separate temporary file:

  ```bash
  gh attestation verify <artifact> --repo llepecki/friendly-envars --predicate-type https://slsa.dev/provenance/v1 --signer-workflow llepecki/friendly-envars/.github/workflows/ci.yml --source-ref refs/heads/master --source-digest <final-sha> --format json
  gh attestation verify <artifact> --repo llepecki/friendly-envars --predicate-type https://spdx.dev/Document/v2.3 --signer-workflow llepecki/friendly-envars/.github/workflows/ci.yml --source-ref refs/heads/master --source-digest <final-sha> --format json
  ```

  Pass the two JSON files, artifact path, and `SHA256SUMS` to the repository verifier's `attestation` subcommand. It must require exactly one verified result in each file, the requested predicate type, the exact artifact filename and SHA-256 subject digest, the exact source repository/ref/commit and signer workflow above, and no additional subject. It must not trust unverified predicate fields as proof of signer identity.
- Package checksum and attestation verification occur immediately before OIDC login/push.
- A failing/compromised `report-tests` job cannot change the package artifact or satisfy a dependency of `publish`.
- Repository gates pass without any NuGet secret. Publication remains blocked until the external operator gates are complete.

### REV-H03 — Enforce one captured environment snapshot

**Required change**

Implement the registration and options-creation lifecycle exactly as defined in the target architecture.

**Acceptance criteria**

- Tests inject a counting `IEnvironmentVariableReader` through the internal core overload. A successful `BindEnvars` reads every selected property exactly once and later option creation performs zero reads. If a read throws a non-cancellation exception, properties through and including the failing property are each read once, later properties are not read, no configurator/marker is registered, and `BindEnvars` throws `EnvarsException(EnvironmentRead)` immediately. An `OperationCanceledException` follows the same read-count/registration rule but propagates reference-equivalent and unwrapped.
- After registration, mutate a value from valid to different-valid to invalid. Existing and newly created `IOptions`, snapshots/scopes, monitor names, and factory instances all continue using the originally captured raw value.
- Named registrations made before and after an environment mutation each retain their own registration-time snapshot.
- Missing and intentionally empty strings retain the documented 1.1.0 behavior.
- A custom binder returns a fresh mutable object for each options instance; instances do not share the converted object.

### REV-H04 — Repair the solution

**Required change**

Remove the duplicate `resources\icon-v2.png` entry. Include every checked-in project in `FriendlyEnvars.slnx` exactly once: library, unit tests, sample, trim smoke, both benchmark executables, and repository verifier.

**Acceptance criteria**

- `dotnet restore FriendlyEnvars.slnx --locked-mode` parses and succeeds from a clean checkout.
- Solution-wide Release build and test commands reach every intended project.
- No solution item or project is duplicated.

### REV-H05 — Produce a valid NuGet package

**Required change**

Pack `resources/icon-v2.png` at package path `/icon-v2.png`, matching `PackageIcon`.

**Acceptance criteria**

- `dotnet pack` succeeds with no warnings.
- `FriendlyEnvars.2.0.0.nupkg` contains `/icon-v2.png`, `/README.md`, `/lib/net8.0/FriendlyEnvars.dll`, and `/lib/net8.0/FriendlyEnvars.xml` exactly once.
- Package inspection reports no `NU5046` or missing metadata asset.

### REV-H06 — Align target frameworks and CI

**Required change**

- Keep the library on `net8.0`.
- Set tests to `net8.0;net10.0`.
- Set the sample to `net8.0;net10.0` and remove all .NET 9 dependency groups.
- Run the full unit suite on Ubuntu for both frameworks.
- Run the environment-name/property-shape/lifecycle subset on Ubuntu, Windows, and macOS for both frameworks.
- Make test-only Microsoft.Extensions references conditional: net8 uses `Microsoft.Extensions.DependencyInjection` 8.0.1 and `Microsoft.Extensions.Options.DataAnnotations` 8.0.0; net10 uses 10.0.11 for both.

**Acceptance criteria**

- CI installs SDK 8.0.424 and SDK 10.0.400 exactly; `global.json` selects 10.0.400. No project or workflow references `net9.0` or a .NET 9 package.
- Both full Ubuntu test targets pass with identical test counts and zero skipped tests.
- The portability subset passes in all six OS/framework combinations.

### REV-H07 — Make the sample a compiled and executed contract

**Required change**

Replace every `BindFromEnvars` with `BindEnvars`, use `DefaultEnvarPropertyBinder`, implement the three-argument binder signature, remove secret-derived output, and include the sample in solution/CI. The host example must call `StartAsync`, resolve/use its service, and call `StopAsync`/dispose. Its default mode returns exit code 0. A `--invalid-validation` mode supplies known-invalid data, requires `StartAsync` to throw `OptionsValidationException`, catches only that expected exception, prints the fixed text `Validation failed during StartAsync as expected.`, and returns exit code 2; reaching service resolution in this mode is a test failure.

**Acceptance criteria**

- `dotnet build` and `dotnet run --no-build` succeed for `net8.0` and `net10.0`.
- Valid configuration reaches `Sample completed successfully!`.
- `eng/run-sample.sh` verifies exit code 0 and the exact success line in default mode, and exit code 2 plus the exact fixed validation line in `--invalid-validation` mode, for both target frameworks.
- Captured stdout/stderr contains neither the full fake password/API key nor any 6-character-or-longer substring from them.

### REV-H08 — Apply correct 2.0 compatibility/versioning

**Required change**

Set version `2.0.0`. Release notes must separate historical 1.1.0 empty-string behavior from these intentional 2.0 breaks: sealed/structured exception behavior; removal of block methods; eager property/name validation; duplicate-registration rejection; registration-time snapshot timing; stricter flags and non-flags enum text grammar; and the `DynamicallyAccessedMembers(PublicProperties)` contract on `BindEnvars<T>`. In the library project set `<EnablePackageValidation>true</EnablePackageValidation>` and `<PackageValidationBaselineVersion>1.1.0</PackageValidationBaselineVersion>`, and include `src/FriendlyEnvars/CompatibilitySuppressions.xml` through an `ApiCompatSuppressionFile` item. Do not add CP/PKV IDs to `NoWarn` and do not enable automatic suppression generation in committed configuration.

**Acceptance criteria**

- Package identity is exactly `FriendlyEnvars.2.0.0` with no unintended suffix in a release build.
- API/package validation compares against published 1.1.0.
- Every `<Suppression>` is scoped to one diagnostic/target, has `<IsBaselineSuppression>true</IsBaselineSuppression>`, and is immediately preceded by an XML comment in the form `REV-ID: justification`; `eng/verify-package.sh` rejects missing REV IDs, blanket targets, unused/duplicate suppressions, and any CP/PKV `NoWarn` entry.
- README, XML documentation, release notes, and package metadata describe the same 2.0 contracts.
- `BehavioralBreakContractTests` asserts that 2.0 rejects non-flags numeric `"-1"` even when `All = -1`, rejects non-flags `"Read,Write"` even when `ReadWrite = 3` is declared, and rejects negative numeric flags text while accepting the corresponding declared member name. Release notes identify all three as intentional differences from 1.1.0.

### REV-H09 — Remove misleading options-blocking APIs

**Required change**

Remove both public methods, both internal boolean settings, the replacement service registrations, and all block-specific tests/documentation. Do not add aliases, obsolete shims, or a type-wide replacement.

**Acceptance criteria**

- Public API inspection contains neither method.
- `IOptions<T>`, `IOptionsSnapshot<T>`, `IOptionsMonitor<T>`, and `IOptionsFactory<T>` resolve normally for default and named options.
- A search limited to `src/`, `tests/`, `sample/`, `benchmarks/`, `README.md`, and package release notes finds no `BlockOptionsSnapshot`, `BlockOptionsMonitor`, `IsOptionsSnapshotAllowed`, or `IsOptionsMonitorAllowed`. API-compatibility baselines/suppressions and this review artifact are explicitly excluded from that search.

### REV-M01 — Reject unsupported property shapes eagerly

**Required change**

During `BindEnvars`, accept only public instance, non-indexed properties with a public instance set/init accessor. Throw a structured `EnvarsException` with `FailureKind.InvalidProperty` before reading any environment values when a decorated property violates the rule.

**Acceptance criteria**

- Getter-only, private-set, protected-set, internal-set, static, and indexed decorated properties fail during `BindEnvars` whether the variable is missing, empty, or populated.
- Failure metadata identifies the exact options type/property/environment name and never includes a value.
- Ordinary public setters and init-only setters, including inherited public properties, continue to bind.
- Static properties remain unchanged after every failure test.

### REV-M02 — Validate every flags-enum bit pattern

**Required change**

Use this grammar after trimming the whole input. Every declared-name match below uses the same deterministic rule: first select an ordinal exact-case match; if none exists, collect all ordinal-case-insensitive matches and accept only when at least one exists and every match has the same underlying-width bit pattern. Reject a case-insensitive match whose candidates have different bit patterns. Apply this rule independently to a single name, every flags-list token, and non-flags enum names.

1. Empty/whitespace-only input is invalid.
2. If the input contains a comma, split on commas, trim each token, require every token to be non-empty and to case-insensitively equal a declared member name, then OR the members. Numeric tokens are forbidden in lists.
3. Without a comma, first match one declared member name case-insensitively; this permits a declared negative-valued member such as `All`.
4. Otherwise accept only ASCII decimal digits matching `[0-9]+`. Leading `+`/`-`, hexadecimal prefixes, signs, separators, and non-ASCII digits are invalid. Parse without overflow into the enum's underlying type.
5. Convert declared values and parsed numeric input to the same-width unsigned bit pattern, OR all declared member patterns into an allowed mask, and reject any result containing a bit outside that mask.

For non-flags enums, trim once and accept only a single declared name or an unsigned ASCII-decimal value for which `Enum.IsDefined` is true; reject commas, signs, hex, overflow, and undefined numeric values.

**Acceptance criteria**

- Test all eight legal enum underlying types.
- Accept declared single names, declared composites, comma-separated declared names, zero, and non-negative numeric combinations fully contained in the allowed mask.
- Reject unknown names, unknown bits, mixed name/unknown input, overflow, negative numeric input, and whitespace-only input.
- Required syntax cases are: accept `" 3 "` when bits 0 and 1 are declared; reject `"+3"`, `"0x3"`, `"Read,2"`, `"1,2"`, empty list elements, and a comma-only value.
- For every non-flags underlying type, assert the exact grammar above: one declared name or one unsigned ASCII-decimal representation of a defined value is accepted; lists, signs (including a declared negative value written numerically), hex, overflow, and undefined values are rejected.
- Define collision fixtures containing `Read = 1`, `READ = 2`, `Same = 4`, and `SAME = 4`. Assert exact-case `Read` and `READ` select their respective values, mixed-case `read` is rejected as conflicting, and mixed-case `same` is accepted as bit-pattern-equivalent. Run the same assertions for flags single names, flags-list tokens, and non-flags names.

### REV-M03 — Enforce null and configuration-stability contracts

**Required change**

Use `ArgumentNullException.ThrowIfNull` for binder and culture. After the configuration delegate returns, capture the binder reference and `CultureInfo.ReadOnly((CultureInfo)culture.Clone())`; never retain/read the mutable settings object during options creation.

**Acceptance criteria**

- Null binder and culture throw synchronously from `BindEnvars` with parameter names `binder` and `culture`, even when no variables exist.
- Mutating the original culture or retaining/changing the settings object after registration cannot alter parsing.
- Binder internal mutability remains explicitly documented as the caller's responsibility.

### REV-M04 — Document the trusted converter/binder boundary

**Required change**

Do not add strict mode. Update README and XML remarks on `IEnvarPropertyBinder`, `UseCustomEnvarPropertyBinder`, and TypeConverter fallback to state that code is trusted, receives complete secret values, may run concurrently, must be deterministic/thread-safe, and must not log or retain input.

**Acceptance criteria**

- All three documentation locations contain the trust, secret, concurrency, and retention warnings.
- Samples contain no binder logging.
- Tests prove one shared binder can be invoked concurrently without library-side serialization and that sanitized wrapping handles its failures.

### REV-M05 — Lock, constrain, and audit dependency restore

**Required change**

- Add repository `NuGet.config` with `<clear />`, only `https://api.nuget.org/v3/index.json`, and package-source mapping `*` to that source. This nuget.org-only rule governs every checked-in project restore.
- Enable lock-file generation and commit lock files for every checked-in project: library, tests, sample, trim smoke, both benchmark executables, and `eng/FriendlyEnvars.RepositoryVerifier`. Transient consumer projects created under the OS temporary directory are excluded.
- CI restores with `--locked-mode`.
- Set `NuGetAudit=true`, `NuGetAuditMode=all`, `NuGetAuditLevel=moderate`, and treat `NU1902`, `NU1903`, and `NU1904` as errors.
- Add weekly Dependabot configuration for NuGet and GitHub Actions.

**Acceptance criteria**

- A clean locked restore succeeds; changing a resolved dependency without updating its lock file fails.
- Checked-in project restores use no configured source other than nuget.org. `eng/smoke-consumer.sh` is the sole exception and must create an isolated temporary `NuGet.config`: map exact package ID `FriendlyEnvars` only to the directory containing the candidate package, and map `Microsoft.*`, `System.*`, `runtime.*`, and `NETStandard.Library` only to nuget.org. Package-add commands use `--no-restore`; the subsequent restore passes `--configfile` explicitly and uses an isolated global-packages folder, so it cannot inherit user-level sources/packages.
- Audit covers transitive packages in every target framework and reports no Moderate-or-higher advisory.
- Analyzer packages remain `PrivateAssets=all`.

### REV-M06 — Remove vulnerable sample dependencies

**Required change**

Use these exact package versions in the sample; use `Microsoft.Extensions.Options` 8.0.2 in the library; use the conditional test versions required by `REV-H06`:

| Target | Dependency versions |
|---|---|
| `net8.0` | `Microsoft.Extensions.DependencyInjection` 8.0.1; `Microsoft.Extensions.Hosting` 8.0.1; `Microsoft.Extensions.Options` 8.0.2; `Microsoft.Extensions.Options.DataAnnotations` 8.0.0; ensure the lock resolves `System.Text.Json` 8.0.6. |
| `net10.0` | Use version 10.0.11 for all four Microsoft.Extensions dependencies. |

Add a direct net8-conditional `System.Text.Json` 8.0.6 reference to the sample and document that it exists solely to enforce the secure transitive floor.

**Acceptance criteria**

- Net8 no longer resolves `System.Text.Json` 8.0.0.
- NuGet audit for both sample targets has no Moderate-or-higher advisory.
- The sample builds and runs under both targets.

### REV-M07 — Eliminate the unload-unsafe global cache

**Required change**

Delete the static `ConcurrentDictionary<Type,...>`. Build metadata once per FriendlyEnvars registration and let the registered configurator own the immutable plan. Do not replace it with `ConditionalWeakTable`, `RuntimeTypeHandle`, or another global collection.

**Acceptance criteria**

- Repository search finds no static cache of `Type`, `PropertyInfo`, attributes, or binding plans.
- A collectible `AssemblyLoadContext` test loads an option type, registers/resolves it, disposes the provider, clears all strong references, performs repeated full GC/finalizer cycles, and requires its weak reference to die.
- Repeating at least ten load/unload generations does not retain prior generations.

### REV-M08 — Remove repeatable reflection work and prove no performance regression

**Required change**

The registration plan must precompute property validation, environment name, target/nullable type, flags metadata, and setter metadata. Options creation must not call `GetProperties`, `GetCustomAttribute`, `CanWrite`, `GetIndexParameters`, `Nullable.GetUnderlyingType`, or `IsDefined(typeof(FlagsAttribute))`. Keep `PropertyInfo.SetValue` and the public binder contract.

Add `tests/FriendlyEnvars.TrimSmoke`, a net8 console project that binds one string and one numeric decorated property, resolves them through the public API, prints only `Trim smoke completed successfully!`, and returns 0. Add `eng/trim-smoke.sh`; on Ubuntu x64 it runs `dotnet restore tests/FriendlyEnvars.TrimSmoke/FriendlyEnvars.TrimSmoke.csproj --runtime linux-x64 --locked-mode`, then `dotnet publish tests/FriendlyEnvars.TrimSmoke/FriendlyEnvars.TrimSmoke.csproj --configuration Release --framework net8.0 --runtime linux-x64 --self-contained true -p:PublishTrimmed=true -p:TrimMode=link -p:TreatWarningsAsErrors=true --no-restore --output <temporary-directory>`. It runs the published executable with fixed non-secret environment values and requires the exact success line and exit code 0.

Add `benchmarks/FriendlyEnvars.Benchmarks` with shared workload source and two net8 executables using BenchmarkDotNet 0.15.8: `FriendlyEnvars.Benchmarks.Baseline` references published `FriendlyEnvars` 1.1.0, and `FriendlyEnvars.Benchmarks.Candidate` references `src/FriendlyEnvars/FriendlyEnvars.csproj`. Both directly reference `Microsoft.Extensions.DependencyInjection` 8.0.1 and differ only in FriendlyEnvars reference and output identity. The shared workload must use only public APIs present in both 1.1.0 and 2.0.0. Benchmark first/cached `IOptions`, repeated factory creation, and snapshot-per-scope using 0/1/10/100 properties and absent/string/numeric/enum/custom-converter values.

Add `eng/compare-benchmarks.sh` and a checked-in net10 `eng/FriendlyEnvars.RepositoryVerifier` console project whose only third-party dependency is YamlDotNet 18.1.0. The verifier exposes exact subcommands `attestation`, `benchmark`, `package`, `docs`, `sbom`, `workflow`, `api-removals`, `package-manifest`, `published-package`, `sourcelink`, and `reproducible-package`. The wrapper mapping is exact: `compare-benchmarks.sh` → `benchmark`; `verify-package.sh` → `package` and `package-manifest`; `verify-docs.sh` → `docs`; `verify-sbom.sh` → `sbom`; `validate-workflows.sh` → `workflow`; `verify-api-removals.sh` → `api-removals`; `verify-published-package.sh` → `published-package`; `verify-sourcelink.sh` → `sourcelink`; and `verify-reproducible-package.sh` → `reproducible-package`. Every wrapper delegates structured inspection rather than parsing binary/XML/JSON/YAML with ad hoc text matching. `eng/verify-repository.sh` only orchestrates the other scripts, `eng/generate-sbom.sh` only invokes the pinned SBOM tool, and `eng/secret-scan.sh` only verifies/invokes the pinned Gitleaks binary, so those three have no verifier subcommand. The benchmark script performs locked restores/builds for both executables, runs them sequentially on the same idle x64 host under the .NET 8 Release runtime using BenchmarkDotNet `ShortRun` plus `MemoryDiagnoser` (one launch, three warmups, three target iterations), writes JSON artifacts to separate temporary directories, and invokes the `benchmark` subcommand. It matches cases by method/property-count/value-scenario and computes `candidate / baseline` independently for mean nanoseconds and allocated bytes/op. Baseline revision `8680adb...` establishes reviewed behavior; published package 1.1.0 is the reproducible benchmark binary.

**Acceptance criteria**

- Tests inject counting reader/observer implementations through the internal core overload, record the counts when `BindEnvars` returns, create 32 options instances through factory/snapshot paths, and assert both counts remain unchanged. They also assert exactly one `PlanBuildStarted()` callback and one `MetadataInspected` callback per selected property.
- Every matched benchmark case must have mean time at or below 1.10x baseline. When baseline allocated bytes/op is greater than zero, candidate allocation must be at or below 1.10x baseline; when baseline allocation is zero, candidate allocation must also be exactly zero and no allocation ratio is computed. For every 10-property value scenario, both `RepeatedFactory` and `SnapshotPerScope` are evaluated independently and at least one metric with a nonzero baseline must be at or below 0.80x baseline. A missing/unmatched case, zero/non-finite elapsed-time baseline, negative/non-finite allocation, BenchmarkDotNet error, or threshold miss fails the script.
- Record raw BenchmarkDotNet artifacts in the implementation handoff; do not commit machine-specific result files.
- The Release build and trimmed smoke test remain warning-free.
- `eng/trim-smoke.sh` leaves no output in the repository; all publish output is under a fresh OS temporary directory that it removes on exit.

### REV-M09 — Make cold construction and binder concurrency deterministic

**Required change**

Eager registration-scoped plan construction replaces concurrent cache construction. Keep one shared custom binder per registration and require it to be thread-safe; do not add locks, factories, or new lifetimes.

**Acceptance criteria**

- A test hook proves metadata discovery executes exactly once per `BindEnvars` call under 32 concurrent option creations.
- A barrier-based test drives 32 concurrent factory/snapshot creations through one binder and observes the exact expected conversion count with no library serialization/deadlock.
- Documentation clearly assigns binder state/thread-safety responsibility to the caller.

### REV-M10 — Enforce a portable environment-name policy

**Required change**

The direct `EnvarAttribute` constructor throws `ArgumentException` with parameter name `name` for null, empty, whitespace-only, `=`-containing, or any `char.IsControl`-containing input. Preserve all other characters, including ordinary embedded spaces and Unicode. During `BindEnvars`, do not instantiate the attribute: decode its constructor value through `CustomAttributeData`, apply the same shared validator, and translate invalid metadata to the exact `InvalidProperty` row/message in the safe exception contract (`EnvironmentVariableName` and `CauseType` both null).

**Acceptance criteria**

- Accepted/rejected corpus tests run on Ubuntu, Windows, and macOS under net8 and net10.
- Exact rejected cases include `null`, `""`, spaces, tabs/newlines only, `"A\0B"`, `"A\nB"`, `"A\tB"`, `"A=B"`, and `"="`; both direct construction and reflected binding behavior are asserted.
- Exact accepted cases include `"A"`, `"A_B1"`, `"A B"`, `"Å_VAR"`, and a 4 KiB valid name. The 4 KiB case validates attribute construction/metadata only because operating systems impose different environment-block limits.

### REV-M11 — Generate, package, and validate complete XML documentation

**Required change**

Enable `GenerateDocumentationFile`. Document every public type/member, including the new failure enum/properties. Do not suppress `CS1591`. Correct empty-string, registration-time snapshot, return-value, null, named-options, precedence, binder trust, and removed-blocking semantics.

**Acceptance criteria**

- Release build produces zero `CS1591` warnings.
- `/lib/net8.0/FriendlyEnvars.xml` exists in the `.nupkg` and contains member entries for every public symbol in the compiled assembly.
- `eng/verify-docs.sh` reflects the packaged assembly, derives every documentable public member ID, and fails if the packaged XML lacks an exact matching `<member>` entry or contains an unresolved/empty summary.
- A documentation consistency test/search finds no claim that environment values refresh, empty values are ignored, fluent methods return new settings, or blocking APIs exist.

### REV-M12 — Make Quick Start independently executable

**Required change**

README Quick Start must include exact `dotnet add package` commands, required namespaces, a complete options type, DI/host registration, valid environment setup, `StartAsync`, resolution, `StopAsync`, and a separate invalid-validation example. State that conversion is automatic and data-annotation validation is opt-in through its companion package/calls. For net8 the documented direct packages are `FriendlyEnvars` 2.0.0, `Microsoft.Extensions.Hosting` 8.0.1, and `Microsoft.Extensions.Options.DataAnnotations` 8.0.0; for net10 they are `FriendlyEnvars` 2.0.0, `Microsoft.Extensions.Hosting` 10.0.11, and `Microsoft.Extensions.Options.DataAnnotations` 10.0.11. Do not add direct DependencyInjection or Options package references to the Quick Start projects; Hosting supplies their required compile assets.

**Acceptance criteria**

- `eng/smoke-consumer.sh` creates clean net8 and net10 console projects in temporary directories, writes the isolated source-mapped `NuGet.config` required by `REV-M05`, executes the exact target-specific package-add commands above with `--no-restore`, restores with the explicit config file, copies only documented code, and builds/runs successfully.
- The invalid example fails during host start with `OptionsValidationException`.
- The smoke projects do not reference repository source projects.

### REV-L01 — Preserve cancellation and distinguish assignment failures

**Required change**

Use separate conversion and assignment try/catch blocks. Propagate `OperationCanceledException` unchanged from either phase. Sanitize every other exception into the safe exception contract with `Conversion` or `Assignment`; never retain the original exception object.

**Acceptance criteria**

- Binder and setter cancellation tests assert reference-equivalent `OperationCanceledException` propagation.
- Throwing binder and setter tests assert different `FailureKind`, exact safe messages, correct `CauseType`, null inner exception, and no secret fragments.

### REV-L02 — Remove redundant DI work and reject duplicates

**Required change**

Remove the no-op `optionsBuilder.Configure`. Reuse one private static readonly `DefaultEnvarPropertyBinder`. Add an internal registration marker and reject a second FriendlyEnvars registration for the same `(T, options name)` synchronously; permit different names.

**Acceptance criteria**

- Service-descriptor tests prove one binding configurator and no no-op configurator per registration.
- Same type/name duplicate registration throws `InvalidOperationException` with exact message `FriendlyEnvars is already registered for options type '{typeof(T).FullName}' and options name '{displayName}'.`, using the safe `displayName` rule from the exception contract.
- Default versus two distinct names register and resolve independently.

### REV-L03 — Remove secret-hygiene traps

**Required change**

- Change ignore rules to `.env*` followed by `!.env.example`.
- Remove secret/prefix output from sample and documentation; use classes or explicitly redacted `ToString` for secret-bearing examples.
- Add `eng/secret-scan.sh`, which downloads Gitleaks 8.30.1 into a temporary directory from the exact initial URL `https://github.com/gitleaks/gitleaks/releases/download/v8.30.1/gitleaks_8.30.1_<platform>.tar.gz`, verifies the release archive SHA-256 before extraction/execution, and runs a full-history scan with redaction and a nonzero exit on findings. Download with HTTPS-only `curl --location --max-redirs 1 --proto '=https' --proto-redir '=https' --fail`; require exactly one redirect and require the effective URL host to be exactly `release-assets.githubusercontent.com`. Reject a direct response, a second redirect, any other host/protocol, or checksum mismatch. `<platform>` may be only `linux_x64` (`551f6fc83ea457d62a0d98237cbad105af8d557003051f41f3e7ca7b3f2470eb`), `darwin_x64` (`dfe101a4db2255fc85120ac7f3d25e4342c3c20cf749f2c20a18081af1952709`), or `darwin_arm64` (`b40ab0ae55c505963e365f271a8d3846efbc170aa17f2607f13df610a9aeb6a5`); fail closed on every other platform, initial URL, filename, or checksum mismatch. CI must use a full-history checkout (`fetch-depth: 0`) in the unprivileged `validate` job. Commit a minimal reviewed allowlist only for obvious fake fixtures.

**Acceptance criteria**

- `.env`, `.env.local`, `.env.production`, and `.env.development` are ignored; `.env.example` is tracked.
- `eng/secret-scan.sh` executes `gitleaks git --redact --no-banner --exit-code 1 --log-opts="--all" .` and passes from a clean full-history checkout; changing the configured version, URL, or checksum causes its self-test to fail.
- Sample stdout/stderr tests exclude full fake values and every 6-character-or-longer substring.

### REV-L04 — Specify and test configuration precedence

**Required change**

Preserve standard registration order and document it. Do not introduce `PostConfigure` or forced environment precedence.

**Acceptance criteria**

- Standard configuration registered before `BindEnvars` is overwritten when a captured environment value is present.
- Standard configuration registered after `BindEnvars` overwrites FriendlyEnvars.
- Missing environment values do not overwrite earlier configuration.
- All cases pass for default and named options.

### REV-L05 — Document complete named-options behavior

**Required change**

Add one compiled README/sample example with two names of the same type. Document per-registration capture time, duplicate rejection, registration-order precedence, and normal access through factory/monitor/snapshot. Do not mention removed blocking APIs.

**Acceptance criteria**

- The example compiles and asserts `IOptionsFactory<T>.Create`, `IOptionsMonitor<T>.Get`, and `IOptionsSnapshot<T>.Get` for both names.
- Each name retains the raw values captured by its own `BindEnvars` call.

### REV-L06 — Ship deterministic symbols and verifiable Source Link

**Required change**

Set deterministic/CI build properties, `PublishRepositoryUrl`, `EmbedUntrackedSources`, `IncludeSymbols`, and `SymbolPackageFormat=snupkg`. Add `Microsoft.SourceLink.GitHub` 8.0.0 with `PrivateAssets=all`. Commit Source Link 8.0.0 and Microsoft SBOM Tool 4.1.5 to the local tool manifest. Update copyright to 2026.

**Acceptance criteria**

- Packing creates one `.nupkg` and one `.snupkg` for 2.0.0.
- Run Source Link release gates only from a clean checkout of the final committed remediation SHA, never from the review baseline revision or an uncommitted tree. `eng/verify-sourcelink.sh` obtains that SHA with `git rev-parse HEAD`, requires `git status --porcelain` to be empty, runs `dotnet sourcelink test` against the `.snupkg`, and requires every repository document URL printed by Source Link to contain that exact 40-character SHA rather than a branch/tag.
- `eng/verify-reproducible-package.sh` creates two temporary clean copies of that same committed tree, performs locked Release build/pack in each, extracts both `.nupkg` and `.snupkg`, and compares the complete sorted relative-path list plus SHA-256 of every extracted payload file. Any missing/extra/different file fails; ZIP container timestamps are the only ignored data.
- Package metadata contains the repository URL and 2026 copyright.

## Repository-wide quality gates

The canonical full repository gate runs on Ubuntu 24.04 x64 from a clean checkout of the final candidate commit, with SDKs 8.0.424 and 10.0.400 installed and no pre-existing `bin`, `obj`, `TestResults`, `artifacts`, or package output. Every checked-in `eng/*.sh` script must have its executable bit committed, use fail-fast shell settings, resolve paths relative to the repository root, create temporary work only with `mktemp -d`, clean it through a trap, and return nonzero on a missing prerequisite or failed assertion. `eng/verify-repository.sh` must fail before executing subordinate gates if `git ls-files --stage 'eng/*.sh'` reports any script without mode `100755`.

Add `eng/verify-repository.sh` as the single local entry point. CI must invoke the same subordinate scripts split across the locked jobs/matrices defined above. `verify-repository.sh` must execute this sequence without omitting or weakening a command:

```bash
dotnet --version # exact output: 10.0.400
dotnet tool restore --configfile NuGet.config
dotnet restore FriendlyEnvars.slnx --locked-mode
dotnet format FriendlyEnvars.slnx --verify-no-changes --no-restore
dotnet build FriendlyEnvars.slnx --configuration Release --no-restore --warnaserror
dotnet test tests/FriendlyEnvars.Tests/FriendlyEnvars.Tests.csproj --configuration Release --framework net8.0 --no-build --no-restore
dotnet test tests/FriendlyEnvars.Tests/FriendlyEnvars.Tests.csproj --configuration Release --framework net10.0 --no-build --no-restore
eng/run-sample.sh
dotnet pack src/FriendlyEnvars/FriendlyEnvars.csproj --configuration Release --no-build --no-restore --output artifacts/release
eng/verify-package.sh artifacts/release/FriendlyEnvars.2.0.0.nupkg artifacts/release/FriendlyEnvars.2.0.0.snupkg
eng/verify-docs.sh artifacts/release/FriendlyEnvars.2.0.0.nupkg
eng/generate-sbom.sh artifacts/release
eng/verify-sbom.sh artifacts/release/sbom.spdx.json
eng/smoke-consumer.sh artifacts/release/FriendlyEnvars.2.0.0.nupkg
eng/trim-smoke.sh
eng/audit-dependencies.sh
eng/validate-workflows.sh
eng/verify-api-removals.sh
eng/secret-scan.sh
eng/verify-sourcelink.sh artifacts/release/FriendlyEnvars.2.0.0.snupkg
eng/verify-reproducible-package.sh
eng/compare-benchmarks.sh
git diff --exit-code
```

`eng/audit-dependencies.sh` must invoke `dotnet list <project> package --vulnerable --include-transitive` for every checked-in `.csproj` and fail on `NU1902`, `NU1903`, `NU1904`, or any reported Moderate/High/Critical advisory. `eng/verify-api-removals.sh` implements the scoped `REV-H09` search. `eng/verify-package.sh` checks the exact package file list/metadata from `REV-H05`, the package-validation baseline/suppressions from `REV-H08`, and the symbol/package properties from `REV-L06`.

Additional CI gates:

- Full tests pass on Ubuntu under net8 and net10 with identical counts and zero skips.
- Mark portability tests with xUnit trait `Category=Portability`; CI runs `dotnet test ... --framework <tfm> --filter 'Category=Portability'` and they pass on Ubuntu, Windows, and macOS under both frameworks.
- Package validation against 1.1.0 has only checked-in, REV-linked intentional-break suppressions.
- `eng/trim-smoke.sh` executes on Ubuntu x64 with the exact RID/publish switches in `REV-M08` and produces zero trim warnings. NativeAOT support is not claimed by 2.0 and is not a release gate.
- Lock files, generated public API baselines, and committed documentation remain unchanged after verification.
- `eng/verify-reproducible-package.sh` and `eng/compare-benchmarks.sh` pass on the same otherwise-idle Ubuntu x64 machine; benchmark failure is release-blocking, not informational.

## External operator gates

These actions require repository/NuGet administration and cannot be completed or waived by an implementation agent:

1. Create GitHub environment `nuget-production` with required reviewer approval and deployment restricted to `master`.
2. On nuget.org, create a trusted publishing policy for owner/repository `llepecki/friendly-envars`, workflow file `ci.yml`, and environment `nuget-production`.
3. Set `NUGET_USER` to the NuGet profile name required by `NuGet/login`; it must not be an email address.
4. Approve one controlled `master` release for the final candidate SHA. Before the NuGet login/push step, require successful `SHA256SUMS` and `gh attestation verify` output for both package files and both predicate types. After publication, download `FriendlyEnvars` 2.0.0 from nuget.org, run `dotnet nuget verify FriendlyEnvars.2.0.0.nupkg --all`, and run `eng/verify-published-package.sh <workflow-nupkg> <nuget-org-nupkg>`. That script must compare the complete extracted path/hash manifest except `.signature.p7s`, because nuget.org repository signing legitimately changes the ZIP container/signature, and must fail on every other difference. Confirm the package owner is `llepecki`.
5. Revoke/delete the old `NUGET_API_KEY` secret after trusted publishing succeeds; confirm MFA and recovery access on the NuGet account.
6. Confirm branch protection requires the complete validation matrix and prevents direct unreviewed pushes to `master`.

Publication is prohibited until all six operator gates are confirmed.

## Implementation handoff requirements

The implementing agent's final report must provide:

- a checklist marking every `REV-*` item complete with links to its implementation and tests;
- the exact commands run and their exit results;
- unit-test counts by framework/OS and confirmation of zero skips;
- package contents, package-validation output, audit output, Source Link output, secret-scan output, and benchmark comparison;
- the list and justification of intentional 1.1.0-to-2.0 API breaks;
- any external operator gate still pending;
- confirmation that no raw environment value was emitted during testing.

The work is not repository-complete if any finding is omitted, any required gate is skipped, an acceptance criterion is replaced with a subjective statement, or a benchmark/security failure is accepted without Captain approval.
