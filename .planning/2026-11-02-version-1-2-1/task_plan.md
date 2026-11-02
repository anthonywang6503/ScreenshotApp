# Task Plan: Bilingual open-source GitHub release 1.3.1

## Goal
Ship English-default English/zh-TW UI, English-first public documentation, tested tagged GitHub release with downloadable portable ZIP.

## Next Step
Commit verified package evidence, tag v1.3.1, rebuild exact tagged source and publish after GitHub CI.

## Current Phase
Phase 5

## Phases

### Phase 1: Requirements & Discovery
- [x] Understand user intent
- [x] Identify constraints
- [x] Document in findings.md
- **Status:** complete

### Phase 2: Planning & Structure
- [x] Define approach
- [x] Reuse existing resources, settings and packaging
- **Status:** complete

### Phase 3: Implementation
- [x] Persist en/zh-TW selection; load culture before services/windows; restart required
- [x] English resources with complete zh-TW satellite and tests
- [x] English-first public docs with preserved Traditional Chinese references
- [x] Version 1.3.1, release notes, portable package and checksums
- **Status:** complete

### Phase 4: Testing & Verification
- [x] Full gate and changed-file format checks
- [x] Public docs links and PowerShell syntax
- [x] Post-commit packaging/resources/ZIP/version verification
- [x] Record package and source evidence
- **Status:** complete

### Phase 5: Delivery
- [x] Review source, docs and preflight packages
- [ ] Commit evidence, tag and rebuild exact final package
- [ ] Fast-forward main, push main/tag, inspect Windows CI
- [ ] Publish release and compare GitHub asset digest
- [ ] Deliver release link to user
- **Status:** in_progress

## Decisions Made
| Decision | Rationale |
|----------|-----------|

## Errors Encountered
| Error | Resolution |
|-------|------------|
