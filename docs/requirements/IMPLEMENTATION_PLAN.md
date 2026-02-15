# Implementation Plan

## Current Job: JOB-075 — Core Unit Tests for All ACs

**Module**: core  
**Priority**: P3  
**Description**: Write unit tests for all core acceptance criteria (workflow, assessment, drift, transitions, back-pressure, git, failures)

### Tasks

- [ ] Study core SPECIFICATION.md for all ACs
- [ ] Map existing tests to ACs  
- [ ] Write tests for any uncovered ACs
- [ ] Verify with gpt-5-mini sub-agent

### Recently Completed Jobs

| Job | Module | Description | Tests |
|-----|--------|-------------|-------|
| JOB-057 | llm | LLM AC tests (all 14 ACs) | 22 tests |
| JOB-101 | llm | Fix IsPremiumModel for -mini variants | bugfix |
| JOB-052 | llm | Task status rejection gate | 14 tests |
| JOB-029 | configuration | Config passthrough | 12 tests |
| JOB-047 | llm | Fresh context window | 2 tests |
| JOB-046 | llm | Copilot SDK auth | 30 tests |
