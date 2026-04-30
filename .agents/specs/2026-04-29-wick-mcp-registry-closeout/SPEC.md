---
id: wick-mcp-registry-closeout-2026-04-29
status: closed-local-owner-paused-publication
owner: HasNoBeef
repo: Wick
branch: feat/mcp-registry-publishing
branch_policy: local-only-public-oss-until-owner-approval
risk: high
requires_network: "only after owner approves PR, NuGet publish, or MCP Registry publish"
requires_secrets:
  - "NUGET_API_KEY only if publishing Wick.Server to nuget.org"
  - "MCP Registry GitHub/OIDC credentials only if publishing server.json"
acceptance_commands:
  - "git status --short --branch"
  - "node -e \"const fs=require('fs'); const j=JSON.parse(fs.readFileSync('server.json','utf8')); const p=j.packages?.[0]; const env=Object.fromEntries((p?.environmentVariables ?? []).map(v => [v.name, v])); const fail=[]; if(j[String.fromCharCode(36)+'schema'] !== 'https://static.modelcontextprotocol.io/schemas/2025-12-11/server.schema.json') fail.push('schema'); if(!/^[-A-Za-z0-9.]+\\/[-A-Za-z0-9._]+$/.test(j.name)) fail.push('name'); if(!j.description || j.description.length > 100) fail.push('description'); if(j.version !== p?.version) fail.push('version'); if(p?.registryType !== 'nuget') fail.push('registryType'); if(p?.registryBaseUrl !== 'https://api.nuget.org/v3/index.json') fail.push('registryBaseUrl'); if(p?.identifier !== 'Wick.Server') fail.push('identifier'); if(p?.runtimeHint !== 'dnx') fail.push('runtimeHint'); if(p?.transport?.type !== 'stdio') fail.push('transport'); if(env.WICK_GROUPS?.default !== 'core') fail.push('WICK_GROUPS default'); if(fail.length) throw new Error(fail.join(', ')); console.log('server.json validation passed');\""
  - "dotnet build Wick.slnx --configuration Release"
  - "dotnet test Wick.slnx --configuration Release"
  - "dotnet pack src/Wick.Server/Wick.Server.csproj --configuration Release --output artifacts/nuget-server"
  - "python3 -m json.tool server.json"
---

# SPEC: Wick MCP Registry Publishing Closeout

## 1. Problem

Wick's current branch, `feat/mcp-registry-publishing`, contains a committed
first pass at publishing Wick.Server as a NuGet-backed MCP Registry server. The
branch is not ready to push, publish, tag, or open as a public PR until the
package, registry metadata, release automation, and public-facing claims are
verified together.

This matters because Wick is public OSS. A bad NuGet or MCP Registry
publication creates externally visible, versioned state that may be immutable or
hard to correct. The branch also sits next to unrelated local agent setup
changes that must be preserved and not accidentally swept into packaging work.

## 2. Goals

- Close out `feat/mcp-registry-publishing` with an owner-approved decision on
  whether it becomes a PR, a local-only branch, or a revised release branch.
- Verify that `Wick.Server` can build, pack as a .NET tool, and install from a
  local package source without writing to global user configuration.
- Validate `server.json` against the current MCP Registry schema and publishing
  rules before any registry publication.
- Reconcile the current release workflow with the new Wick.Server package path.
- Keep Wick's public claims honest: registry, NuGet, README, and release notes
  must only describe behavior that was freshly verified.
- Preserve all existing local and untracked changes not owned by this closeout.
- Explicitly block push, publish, tag, release, and PR creation until the owner
  approves that public OSS action.
- Resolve the Opus 4.7 spec review blockers before touching package/release
  files.

## 3. Non-Goals

- Do not publish to NuGet, the MCP Registry, GitHub releases, or any other
  external service without explicit owner approval.
- Do not open a PR or push the branch without explicit owner approval.
- Do not include unrelated agent-control files in a public Wick PR unless the
  owner separately approves that public-facing rollout.
- Do not solve GDScript-side bridge auth, second public-test pass work,
  discoverability fixes, or docs drift closeout in this branch.

## 4. Current System Facts

- Owner instruction for this session limits edits to this file:
  `Wick/.agents/specs/2026-04-29-wick-mcp-registry-closeout/SPEC.md`.
- `AGENTS.md` says Wick is public OSS, requires an approved `SPEC.md` for
  non-trivial work, uses PR-only workflow, forbids direct pushes to `main`, and
  requires fresh verification before completion claims.
- `AGENTS.md` defines the canonical product verification gate:
  `dotnet build Wick.slnx --configuration Release && dotnet test Wick.slnx --configuration Release`.
- `STATUS.md` frontmatter says Wick is in `phase-2-public-testing`, version
  `1.0.0`, target framework `net10.0`, .NET SDK `10.0.201`, and 245/245 tests
  passing as of `2026-04-19T00:00-07:00`.
- `.agents/DOCUMENTATION_GUIDE.md` says Wick `.agents/specs/` is for
  agent/Symphony task-control specs and should be kept local unless approved;
  durable public docs belong under repo-native `docs/` paths.
- `.agents/specs/2026-04-29-realignment-handoff/SPEC.md` classifies Wick's
  branch policy as `local-only-public-oss` and recommends a closeout spec for
  `feat/mcp-registry-publishing` before public action.
- `.agents/specs/2026-04-29-repo-audit/SPEC.md` lists dotnet tool packaging as
  proposed work and says the active local branch is
  `feat/mcp-registry-publishing`.
- `git status --short --branch` observed:
  ```text
  ## feat/mcp-registry-publishing
   M .gitignore
   M AGENTS.md
  ?? .agents/
  ?? .claude/
  ?? CLAUDE.md
  ?? WORKFLOW.md
  ```
- `git log --oneline --decorate -n 12` shows branch head:
  `6d4f75b (HEAD -> feat/mcp-registry-publishing) feat: package Wick.Server as a NuGet dotnet tool + add server.json`.
- `git diff --name-status origin/main...HEAD` shows the branch commit changes:
  ```text
  A	server.json
  A	src/Wick.Server/README.md
  M	src/Wick.Server/Wick.Server.csproj
  ```
- `git diff --stat origin/main...HEAD` reports 3 files changed and 157
  insertions on the committed branch delta.
- `git diff --name-status` shows unrelated local modifications to `.gitignore`
  and `AGENTS.md`; `git ls-files --others --exclude-standard` shows untracked
  `.agents/`, `.claude/`, `CLAUDE.md`, and `WORKFLOW.md`.
- `server.json` currently declares schema
  `https://static.modelcontextprotocol.io/schemas/2025-12-11/server.schema.json`,
  name `io.github.buildepicshit/wick`, version `1.0.0`, package
  `Wick.Server` version `1.0.0`, `registryType: "nuget"`, `runtimeHint: "dnx"`,
  and stdio transport.
- Command:
  `node -e "const fs=require('fs'); const j=JSON.parse(fs.readFileSync('server.json','utf8')); console.log(j.description.length);"`
  returned `261`.
- The official `2025-12-11` MCP Registry schema sets `ServerDetail.description`
  `maxLength` to `100`, so the current `server.json` description length is a
  likely schema violation that must be fixed or explained before publish.
- `src/Wick.Server/Wick.Server.csproj` sets `IsPackable`, `PackAsTool`,
  `ToolCommandName` `wick-server`, `PackageId` `Wick.Server`,
  `PackageReadmeFile` `README.md`, and `GeneratePackageOnBuild` `false`.
- `src/Wick.Server/README.md` includes
  `<!-- mcp-name: io.github.buildepicshit/wick -->`, documents `dnx
  Wick.Server@1.0.0`, and documents `dotnet tool install --global Wick.Server`.
- `.github/workflows/release.yml` currently says it is a tag-driven NuGet
  release for `Wick.Runtime`; its job name is "Pack Wick.Runtime" and it packs
  `src/Wick.Runtime/Wick.Runtime.csproj`, not `src/Wick.Server/Wick.Server.csproj`.
- `src/Wick.Server/Wick.Server.csproj` comments say packing happens on tag push
  via `release.yml`, which conflicts with the current workflow content.
- `nuget.config` uses `<clear/>` and a single `nuget.org` package source.
- Official MCP Registry docs checked on 2026-04-29 say the registry is in
  preview, uses `server.json`, and publishes metadata through `mcp-publisher`.
- Official MCP Registry package-type docs say NuGet packages use
  `registryType: "nuget"` and the official NuGet registry, and ownership
  verification checks the package README for `mcp-name: $SERVER_NAME`.
- Official MCP Registry authentication docs say GitHub-based names must use
  `io.github.username/*` or `io.github.orgname/*`; Wick's current
  `io.github.buildepicshit/wick` name implies GitHub org authorization.
- Official MCP Registry versioning docs say the server version must be unique
  for each publication, metadata cannot be changed once published, and local
  server versions should align with underlying package versions.
- Official MCP Registry GitHub Actions docs show OIDC publication support, but
  any workflow addition is public release automation and requires owner approval
  before push.
- Owner approved local execution in the current root session on 2026-04-29, but
  did not approve push, PR, tag, NuGet publish, or MCP Registry publish.
- Owner triage approval on 2026-04-30 marks the local MCP Registry handoff
  closed and keeps public publish/auth owner-paused.
- Claude Opus 4.7 reviewed this spec and blocked execution until concrete
  decisions were recorded for description text, `WICK_GROUPS`, schema
  validation, release workflow strategy, and smoke-test commands.
- `command -v dnx` returns `/home/hasnobeef/.dotnet/dnx`, and `dnx --help`
  confirms local .NET 10 SDK support for `dnx <packageId>@<version>`.
- Current unrelated local diff snapshot for `.gitignore` and `AGENTS.md`:
  `git diff --no-color -- .gitignore AGENTS.md | sha256sum` returned
  `2a386b09420dcc693f8232da537862f8c1641920a420329506637cd44cdac5bd`.

## 5. Desired Behavior

After the approved closeout is executed, the branch should be in one of these
explicit states:

- **Ready for owner-approved public PR:** build, tests, local pack, local tool
  install, registry metadata validation, and workflow/docs consistency are all
  verified and recorded.
- **Needs revision before PR:** specific package, schema, workflow, or public
  docs issues are listed in the completion report with no publish or PR.
- **Abandoned/superseded locally:** owner decides not to publish Wick.Server via
  this branch; rollback or supersession steps are documented without deleting
  unrelated local work.

The desired publish path, if approved later, is:

1. `Wick.Server` package is published to nuget.org only after local pack and
   install smoke tests pass.
2. MCP Registry metadata is published only after the NuGet package exists and
   the package README contains the matching `mcp-name` marker.
3. GitHub PR, tag, NuGet publish, and MCP Registry publish are separate
   owner-approved public actions with recorded evidence.

## 6. Domain Model / Contract

- `server.json` is the MCP Registry metadata contract. It must satisfy the
  linked JSON schema and current official publishing rules.
- Registry server name: `io.github.buildepicshit/wick`.
- Registry description: `Roslyn-enriched C# exception telemetry for Godot over MCP.`
  This is 58 characters and satisfies the schema's 100-character limit.
- Underlying package: NuGet package `Wick.Server`.
- Package command: .NET tool command `wick-server`.
- Runtime hint: `dnx`; keep this because `dnx` is present in the pinned .NET 10
  SDK environment and is the registry install path this branch advertises.
- Transport: stdio.
- Version contract: `server.json` version, package version, and
  `Directory.Build.props` `<Version>` stay aligned at `1.0.0` for local
  validation. Public publish still requires owner confirmation.
- Ownership verification contract: the packaged `README.md` must contain
  `mcp-name: io.github.buildepicshit/wick`.
- Release workflow contract: update the existing tag-driven release workflow to
  pack both `Wick.Runtime` and `Wick.Server`. This preserves shared repo version
  semantics and keeps `Wick.Server.csproj`'s release comment true.
- Environment variable contract:
  - `WICK_GODOT_BIN` points to a Godot 4.6.1+ .NET/Mono binary.
  - `WICK_PROJECT_PATH` points to a Godot project root containing
    `project.godot`.
  - `WICK_GROUPS` activates Wick tool pillars and defaults to `core` in
    `server.json`, matching `AGENTS.md`, code behavior, and the package README.
- Documentation contract: this local closeout may update package surfaces and
  release automation. Broader public docs stay unchanged unless verification
  proves they are false.
- Agent-control contract: `.agents/` and `.claude/` remain local in this
  closeout. A public-facing rollout would require a separate spec.
- Registry auth contract: GitHub auth is the expected namespace fit for
  `io.github.buildepicshit/wick`, but auth/publish is deferred until explicit
  owner approval.

## 7. Interfaces And Files

Expected implementation touch points after approval:

- `server.json`
- `src/Wick.Server/Wick.Server.csproj`
- `src/Wick.Server/README.md`
- `.github/workflows/release.yml` or a new dedicated MCP/NuGet publish workflow
- `Directory.Build.props` if a version bump is required
- `CHANGELOG.md`, `STATUS.md`, `README.md`, or `docs/getting-started.md` only
  if public claims need to match the packaging change

Interfaces affected after approval:

- NuGet package metadata for `Wick.Server`.
- .NET tool command `wick-server`.
- MCP Registry `server.json` metadata.
- GitHub Actions release/publish automation.
- Public installation instructions for users and MCP clients.

Files intentionally out of scope for this spec-writing task:

- Wick product code.
- Public docs outside `.agents/specs/`.
- Root workspace files.
- User/global config.
- Existing unrelated local changes in `.gitignore`, `AGENTS.md`, `.agents/`,
  `.claude/`, `CLAUDE.md`, and `WORKFLOW.md`.

## 8. Execution Plan

1. Owner approval for local execution has been given. Public actions remain
   blocked until separately approved.
2. Reconfirm branch hygiene with `git status --short --branch`,
   `git diff --name-status`, `git ls-files --others --exclude-standard`, and
   `git log --oneline --decorate origin/main..HEAD`. Preserve unrelated local
   changes and do not stage them.
3. Validate `server.json` locally:
   - parse as JSON;
   - validate local fields against the schema URL embedded in `$schema`;
   - reduce `description` to the approved 58-character copy;
   - confirm `registryType`, `registryBaseUrl`, `identifier`, `version`,
     `runtimeHint`, transport, and environment variables match current docs;
   - set `WICK_GROUPS.default` to `core`.
4. Verify package metadata:
   - confirm `PackageId`, `ToolCommandName`, `PackageReadmeFile`, license,
     repository, and package tags are correct;
   - inspect the generated `.nupkg` to confirm `README.md` is included at the
     expected path and includes the matching `mcp-name` marker.
5. Run local product verification after approval:
   - `dotnet build Wick.slnx --configuration Release`;
   - `dotnet test Wick.slnx --configuration Release`.
6. Run a local package smoke test after approval:
   - `dotnet pack src/Wick.Server/Wick.Server.csproj --configuration Release --output artifacts/nuget-server`;
   - install `Wick.Server` from `artifacts/nuget-server` into a temporary
     `--tool-path` under `/tmp`;
   - launch `wick-server --groups=core` only under a bounded timeout or MCP
     stdio harness and record the observed startup behavior.
7. Resolve the release workflow mismatch by updating `release.yml` to pack both
   `Wick.Runtime` and `Wick.Server`, upload both package artifacts, and publish
   whatever packages are present only when the owner later approves release.
8. Decide public docs scope:
   - keep package README and registry metadata truthful;
   - keep broader public docs unchanged unless local verification proves they
     are false;
   - avoid noisy public agent-control documentation.
9. Prepare a PR only after owner approval:
   - stage explicit files by name;
   - use a conventional PR title;
   - include fresh local verification output and residual risk;
   - do not include unrelated local agent setup files unless separately
     approved.
10. Publish only after owner release approval:
    - confirm PR merged and CI green;
    - publish/tag using the approved workflow;
    - publish MCP Registry metadata only after the NuGet package is available;
    - verify registry search/API output for `io.github.buildepicshit/wick`.

## 9. Safety Invariants

- No `git push`, PR creation, GitHub release, tag push, NuGet publish, or
  `mcp-publisher publish` may run until the owner explicitly approves that
  public action.
- Do not stage, overwrite, revert, format, or delete unrelated local changes.
- Do not use `git add .`; stage explicit files only during any later approved
  PR work.
- Do not write to global user config for tool smoke tests; use `/tmp` and
  `--tool-path` or another owner-approved disposable path.
- Do not commit secrets, NuGet API keys, registry tokens, MCP auth material, or
  generated credential files.
- Keep Wick on .NET 10 and `net10.0`; do not downshift frameworks or
  multi-target to satisfy package tooling.
- Preserve Wick's external-process architecture; no packaging change may load
  Wick.Server into Godot.
- Treat MCP Registry and NuGet published versions as externally visible and
  hard to mutate after release.
- If the registry schema, package README marker, package install smoke, or
  release workflow cannot be made consistent, stop before public action.

## 10. Test Plan

Do not run these commands during the spec-writing task. They are the closeout
execution gates after owner approval.

Local state and branch hygiene:

```bash
git status --short --branch
git diff --name-status
git ls-files --others --exclude-standard
git log --oneline --decorate origin/main..HEAD
```

Product build and tests:

```bash
dotnet build Wick.slnx --configuration Release
dotnet test Wick.slnx --configuration Release
```

Registry metadata sanity:

```bash
python3 -m json.tool server.json
node -e "const fs=require('fs'); const j=JSON.parse(fs.readFileSync('server.json','utf8')); console.log(j.description.length);"
node -e "const fs=require('fs'); const j=JSON.parse(fs.readFileSync('server.json','utf8')); const p=j.packages?.[0]; const env=Object.fromEntries((p?.environmentVariables ?? []).map(v => [v.name, v])); const fail=[]; if(j[String.fromCharCode(36)+'schema'] !== 'https://static.modelcontextprotocol.io/schemas/2025-12-11/server.schema.json') fail.push('schema'); if(!/^[-A-Za-z0-9.]+\\/[-A-Za-z0-9._]+$/.test(j.name)) fail.push('name'); if(!j.description || j.description.length > 100) fail.push('description'); if(j.version !== p?.version) fail.push('version'); if(p?.registryType !== 'nuget') fail.push('registryType'); if(p?.registryBaseUrl !== 'https://api.nuget.org/v3/index.json') fail.push('registryBaseUrl'); if(p?.identifier !== 'Wick.Server') fail.push('identifier'); if(p?.runtimeHint !== 'dnx') fail.push('runtimeHint'); if(p?.transport?.type !== 'stdio') fail.push('transport'); if(env.WICK_GROUPS?.default !== 'core') fail.push('WICK_GROUPS default'); if(fail.length) throw new Error(fail.join(', ')); console.log('server.json validation passed');"
```

Package smoke:

```bash
dotnet pack src/Wick.Server/Wick.Server.csproj --configuration Release --output artifacts/nuget-server
pkg="$(find artifacts/nuget-server -name 'Wick.Server.*.nupkg' ! -name '*.symbols.nupkg' | head -1)"
test -n "$pkg"
unzip -l "$pkg"
unzip -p "$pkg" README.md | rg -n "mcp-name: io.github.buildepicshit/wick"
mkdir -p /tmp/wick-server-tool-smoke
dotnet tool install Wick.Server --version 1.0.0 --add-source artifacts/nuget-server --tool-path /tmp/wick-server-tool-smoke
timeout 10 /tmp/wick-server-tool-smoke/wick-server --groups=core < /dev/null
dnx --help
```

MCP Registry publish readiness, not publication:

```bash
mcp-publisher --help
```

Manual checks:

- Confirm the current official MCP Registry docs still reference the schema URI
  in `server.json`.
- Confirm `server.json` description length and field names satisfy the schema.
- Confirm `Wick.Server` exists or is approved to be created on nuget.org before
  registry publish.
- Confirm GitHub authorization for `io.github.buildepicshit/wick`.
- Confirm release workflow behavior packs both `Wick.Runtime` and
  `Wick.Server` intentionally.
- Confirm no unrelated local agent setup files are included in public PR scope.

## 11. Acceptance Criteria

- [x] Owner approved local execution of this closeout spec.
- [x] No push, PR, tag, release, NuGet publish, or MCP Registry publish occurred
      before owner approval.
- [x] Branch hygiene was recorded and unrelated local changes were preserved.
- [x] `server.json` validates against the current official MCP Registry schema.
- [x] `server.json` name, version, NuGet package reference, transport, runtime
      hint, and environment variables match current official guidance or have a
      recorded owner-approved exception.
- [x] `src/Wick.Server/README.md` is present in the package and contains the
      matching `mcp-name` marker.
- [x] `Wick.Server` packs as a .NET tool and installs from a local package
      source into a temporary tool path.
- [x] `dotnet build Wick.slnx --configuration Release` passes.
- [x] `dotnet test Wick.slnx --configuration Release` passes.
- [x] The `release.yml` versus `Wick.Server.csproj` publishing mismatch is
      resolved by packing both Wick packages.
- [x] Public docs and changelog claims are accurate for what was verified.
- [x] Completion report records files changed, commands run, verification
      output, residual risk, and any spec evidence candidates.

## 12. Rollback Plan

Before public publication:

- Close or abandon the local branch by owner decision.
- Revert or supersede the branch commit in a new local commit, preserving
  unrelated local changes.
- Delete generated package artifacts only with explicit scope and owner
  approval if they are tracked or outside disposable directories.

After PR but before external publish:

- Close the PR or push a corrective commit after owner approval.
- Do not tag or publish until CI and local evidence are clean.

After NuGet or MCP Registry publication:

- Treat the published version as externally visible and potentially immutable.
- Publish a new corrected version rather than mutating history.
- If NuGet supports package deprecation for the published artifact, use it only
  after owner approval.
- Publish a new MCP Registry version with corrected metadata if registry
  metadata cannot be changed in place.

## 13. Open Questions

- [x] Use `Wick.Server` version `1.0.0` for local validation; public publish
      still requires owner confirmation.
- [x] Update existing `release.yml` to pack both `Wick.Runtime` and
      `Wick.Server`.
- [x] Keep `runtimeHint: "dnx"` and verify `dnx` is available.
- [x] Set `WICK_GROUPS.default` to `core`.
- [x] Limit public docs scope to package surfaces and release workflow unless
      verification proves broader public docs are false.
- [ ] Which owner-approved authentication method should be used for MCP
      Registry publication: GitHub OAuth, GitHub OIDC, DNS, or HTTP?
- [x] Keep local `.agents/` and `.claude/` files out of this public closeout; a
      public-facing rollout needs a separate spec.

## 14. Completion Report

To be filled by the executor/verifier:

- Files changed:
  - `server.json`
    - shortened `description` from 261 characters to the approved 58-character
      copy required by the MCP Registry schema's 100-character maximum;
    - changed `WICK_GROUPS.default` from `core,runtime,csharp,build` to `core`.
  - `.github/workflows/release.yml`
    - updated the tag-driven release workflow from Wick.Runtime-only packaging
      to packaging both `Wick.Runtime` and `Wick.Server`;
    - renamed package artifacts from `wick-runtime-nupkg-*` to
      `wick-nuget-packages-*`.
  - `.agents/specs/2026-04-29-wick-mcp-registry-closeout/SPEC.md`
    - recorded Opus review blockers, owner-local-execution approval, execution
      decisions, acceptance status, and verification evidence.
  - `.agents/specs/2026-04-29-realignment-handoff/SPEC.md`
    - updated the next-agent handoff to review and execute this approved
      closeout spec locally.
- Commands run:
  - `git status --short --branch`
    - PASS; branch `feat/mcp-registry-publishing`; unrelated local changes
      still present and preserved: `.gitignore`, `AGENTS.md`, `.agents/`,
      `.claude/`, `CLAUDE.md`, `WORKFLOW.md`.
  - `git diff --name-status`
    - PASS; tracked local diff currently includes `.github/workflows/release.yml`,
      `.gitignore`, `AGENTS.md`, and `server.json`.
  - `git ls-files --others --exclude-standard`
    - PASS; untracked agent-control files remain local and unstaged.
  - `git log --oneline --decorate origin/main..HEAD`
    - PASS; branch head remains
      `6d4f75b (HEAD -> feat/mcp-registry-publishing) feat: package Wick.Server as a NuGet dotnet tool + add server.json`.
  - `python3 -m json.tool server.json`
    - PASS; `server.json` parses as JSON.
  - `node -e "const fs=require('fs'); const j=JSON.parse(fs.readFileSync('server.json','utf8')); console.log(j.description.length);"`
    - PASS; output `58`.
  - `node -e "const fs=require('fs'); const j=JSON.parse(fs.readFileSync('server.json','utf8')); const p=j.packages?.[0]; const env=Object.fromEntries((p?.environmentVariables ?? []).map(v => [v.name, v])); const fail=[]; if(j[String.fromCharCode(36)+'schema'] !== 'https://static.modelcontextprotocol.io/schemas/2025-12-11/server.schema.json') fail.push('schema'); if(!/^[-A-Za-z0-9.]+\\/[-A-Za-z0-9._]+$/.test(j.name)) fail.push('name'); if(!j.description || j.description.length > 100) fail.push('description'); if(j.version !== p?.version) fail.push('version'); if(p?.registryType !== 'nuget') fail.push('registryType'); if(p?.registryBaseUrl !== 'https://api.nuget.org/v3/index.json') fail.push('registryBaseUrl'); if(p?.identifier !== 'Wick.Server') fail.push('identifier'); if(p?.runtimeHint !== 'dnx') fail.push('runtimeHint'); if(p?.transport?.type !== 'stdio') fail.push('transport'); if(env.WICK_GROUPS?.default !== 'core') fail.push('WICK_GROUPS default'); if(fail.length) throw new Error(fail.join(', ')); console.log('server.json validation passed');"`
    - PASS; output `server.json validation passed`.
  - `dotnet build Wick.slnx --configuration Release`
    - PASS outside sandbox; `Build succeeded`, 0 warnings, 0 errors.
  - `dotnet test Wick.slnx --configuration Release`
    - PASS outside sandbox; 245 passed, 0 failed, 0 skipped.
  - `dotnet pack src/Wick.Server/Wick.Server.csproj --configuration Release --output artifacts/nuget-server`
    - PASS outside sandbox; refreshed
      `artifacts/nuget-server/Wick.Server.1.0.0.nupkg`.
  - `unzip -l artifacts/nuget-server/Wick.Server.1.0.0.nupkg`
    - PASS; package includes root `README.md` and
      `tools/net10.0/any/Wick.Server.dll`.
  - `zipgrep -n "mcp-name: io.github.buildepicshit/wick" artifacts/nuget-server/Wick.Server.1.0.0.nupkg README.md`
    - PASS; output `README.md:7:<!-- mcp-name: io.github.buildepicshit/wick -->`.
  - `dotnet tool install Wick.Server --version 1.0.0 --add-source artifacts/nuget-server --tool-path /tmp/wick-server-tool-smoke.J0oSHYKt --ignore-failed-sources`
    - PASS with isolated `DOTNET_CLI_HOME` and `NUGET_PACKAGES` under `/tmp`;
      output `Tool 'wick.server' (version '1.0.0') was successfully installed.`
  - `timeout 10 /tmp/wick-server-tool-smoke.J0oSHYKt/wick-server --groups=core`
    - PASS outside sandbox; Wick started with active tool group `core` and
      exited with code 0 when stdin closed. Bridge connection warnings were
      expected because no Godot bridge was running.
  - `dnx --help`
    - PASS; confirmed .NET 10 `dnx` is available.
  - `timeout 10 env DOTNET_CLI_HOME=/tmp/wick-dnx-home.J0oSHYKt NUGET_PACKAGES=/tmp/wick-dnx-packages.J0oSHYKt dnx Wick.Server@1.0.0 --yes --source artifacts/nuget-server -- --groups=core`
    - PASS outside sandbox; `dnx` installed from the local package source,
      started Wick with active tool group `core`, and exited with code 0 when
      stdin closed. Bridge connection warnings were expected because no Godot
      bridge was running.
  - `dotnet pack src/Wick.Runtime/Wick.Runtime.csproj --configuration Release --no-build --no-restore --output artifacts/nuget`
    - PASS outside sandbox; created `Wick.Runtime.1.0.0.nupkg` and
      `Wick.Runtime.1.0.0.snupkg`.
  - `dotnet pack src/Wick.Server/Wick.Server.csproj --configuration Release --no-build --no-restore --output artifacts/nuget`
    - PASS outside sandbox; created `Wick.Server.1.0.0.nupkg`.
  - `git diff --check`
    - PASS; no whitespace errors.
  - `command -v mcp-publisher`
    - NOT INSTALLED; no registry publication attempted.
- Verification result:
  - Local closeout verification passed.
  - `server.json` satisfies the current checked schema constraints used by this
    spec: schema URI, reverse-DNS name pattern, description length, aligned
    server/package version, NuGet registry metadata, `dnx` runtime hint, stdio
    transport, and `WICK_GROUPS.default=core`.
  - Wick product build and test gates passed with 0 warnings and 245/245 tests
    passing.
  - `Wick.Server` packs, installs from a local source into `/tmp`, and starts
    through both `wick-server` and `dnx`.
- Public actions taken:
  - None. No push, PR, tag, GitHub release, NuGet publish, or MCP Registry
    publish was performed.
- Final closeout status:
  - Local MCP Registry packaging handoff is closed.
  - Public PR, tag, NuGet publish, MCP Registry publish, and auth method choice
    are owner-paused.
- Green room status:
  - `allowed-local-only`.
- Anything intentionally left untouched:
  - Existing unrelated local tracked changes in `.gitignore` and `AGENTS.md`.
  - Existing untracked local agent-control files under `.agents/`, `.claude/`,
    `CLAUDE.md`, and `WORKFLOW.md`.
  - Public docs outside package metadata and release automation.
  - `src/Wick.Server/Wick.Server.csproj` and
    `src/Wick.Server/README.md`, because their metadata and MCP marker already
    matched the approved closeout decisions.
- Residual risk:
  - Public publication remains blocked until owner approval chooses the auth
    method and release path.
  - `mcp-publisher` is not installed locally, so registry CLI dry-run/help was
    not verified in this closeout.
  - The local Wick smoke tests ran without a Godot bridge; startup and package
    execution were verified, but end-to-end bridge behavior was not part of
    this packaging closeout.
  - Generated package artifacts under `artifacts/` are local verification
    outputs and should not be staged unless a later release spec explicitly
    calls for them.
- Spec evidence candidates:
  - For NuGet-backed MCP servers, keep `server.json` description under the
    schema's 100-character cap and keep the package README ownership marker in
    the packed artifact, not just in the source tree.
  - For Wick release automation, pack `Wick.Runtime` and `Wick.Server` together
    from the shared repo version to avoid a split public release state.
  - For package smoke tests, use isolated `/tmp` `DOTNET_CLI_HOME`,
    `NUGET_PACKAGES`, and `--tool-path` so agent verification does not mutate
    global tool state.
