# Production TODO — Mind Unlocking Academy WhisperWrap Block Zero

## Current architecture summary
- New modular monolith solution with API, Application, Domain, Infrastructure, Contracts, Workers, and test projects.
- API owns transport concerns: version prefix `/api/v1`, JWT authentication hook-up, permission policies, security headers, rate limiting, ProblemDetails, Swagger, and health endpoints.
- Domain owns deterministic business rules already started for content workflow, question-attempt state transitions, and readiness scoring.
- Infrastructure owns EF Core/Identity persistence foundations plus refresh-session and outbox tables.
- Workers project contains the durable outbox-processing host boundary.

## Missing production requirements and risks
- Auth endpoints are mapped but still need full registration, email verification, JWT issuance, refresh-token rotation, reuse detection, MFA enforcement, forgot/reset password, logout, and revocation. Risk: cannot safely authenticate users.
- EF migrations and full domain schema are not generated because this environment lacks the .NET SDK. Risk: database cannot be deployed yet.
- W1/W2/W3 service and persistence are only domain/contract foundations. Risk: no complete learning flow yet.
- Azure services, AI/RAG, certificates, notifications, bicep modules, and CI/CD are scaffolds or pending. Risk: not deployable to Azure production yet.
- Integration, architecture, functional, and security suites must be expanded after runtime implementation.

## Phase 1 — Foundation
- [x] Solution structure.
- [x] Strongly typed option classes.
- [x] EF Core/Identity persistence foundation.
- [x] Permission constants and authorization policy registration.
- [x] ProblemDetails, Swagger, rate limiting, security headers, health endpoints.
- [x] Initial domain tests for readiness, question idempotency, W1 leakage contract.
- [ ] Implement complete auth use cases and token services.
- [ ] Generate initial EF Core migration.
- [ ] Run dotnet format/build/test in an environment with .NET 8 SDK.

## Phase 2 — Learning core
- [ ] Challenge/cohort/enrollment schema and use cases.
- [ ] Learning packs, capsules, questions, immutable content versions.
- [ ] W1 challenge projection with no answer leakage.
- [ ] Idempotent answer submission with server scoring.
- [ ] W3 acknowledgement and capsule completion transaction/outbox.

## Phase 3 — Engagement and readiness
- [ ] Check-ins with uniqueness and edit history.
- [ ] Team accountability privacy-filtered projections.
- [ ] Support requests.
- [ ] Persist deterministic readiness formula versions and assessments.

## Phase 4 — Outcomes
- [ ] Scenarios, rehearsal selection, rewards, raffles, certificates, notifications.

## Phase 5 — Administration and governance
- [ ] Content review workflow endpoints, reports, audit log coverage.

## Phase 6 — AI governance
- [ ] Approved-content RAG through Azure AI Search/OpenAI, Content Safety, AI audit records, human approval workflow.

## Phase 7 — Azure production readiness
- [ ] Complete Bicep modules, GitHub Actions CI/CD, Docker build, smoke tests, operational runbooks, disaster recovery.
