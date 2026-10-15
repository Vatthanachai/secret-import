# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

@AGENTS.md

## Claude Code subagents

Project subagents live in `.claude/agents/`. Use the one that owns the area you're changing:

| Agent | Owns | Plan phases |
|---|---|---|
| `core-tdd` | `SecretImport.Core` + `SecretImport.Core.Tests`, test-first | 0–5 |
| `cli-builder` | `SecretImport.Cli` | 6 |
| `avalonia-ui` | `SecretImport.Desktop` | 7 |
| `release-ci` | `.github/workflows/`, packaging, install docs | 8 |
| `adr-keeper` | `obsidian/` vault: checks code against the ADRs and keeps the docs current | any |
