# Claude Session Finder

A Spotlight-style launcher for your Claude Code sessions: press a global hotkey, type a few words, and it tells you **which folder** you started that session from, then opens it, resumes it, or copies it.

Runs on **Windows** (WPF) and **macOS** (Avalonia). Both heads share the same engine, settings format and behaviour.

## TL;DR

- Global hotkey → type → get **title · last activity · folder · branch** for every matching session.
- Default hotkey: `Ctrl+Alt+Space` on Windows, `Option+Space` on macOS. If that chord is already taken, the app falls back to the next free one (for example `Ctrl+Alt+K`). The tray/menu-bar icon always shows the chord that was actually registered.
- Four actions on the selected result: open in VS Code, resume (`claude --resume`), reveal in Explorer/Finder, copy.
- Every setting (hotkey, ranking weights, behaviour) applies **without a restart**. The only exception is moving the index file.
- The index is a rebuildable cache: if it gets corrupted, the app deletes it and rebuilds it on its own (about 1.5 s for ~100 sessions).

---

## What it does

The piece of information you need — the folder you launched the session from — exists on disk but is hard to get at: the folder names under `~/.claude/projects` are encoded lossily. This app parses the transcripts, indexes the searchable text in SQLite/FTS5, and gives you back **title · last activity · folder · branch**.

## Daily use

| Windows | macOS | What it does |
|---|---|---|
| `Ctrl+Alt+Space` (or the registered chord) | `⌥Space` (or the registered chord) | Open / close the search box |
| `↑` `↓` | `↑` `↓` | Move the selection |
| `Enter` | `Enter` | Open the folder in VS Code |
| `Ctrl+Enter` | `⌘Enter` | Resume the session (`claude --resume` in that folder) |
| `Ctrl+Shift+Enter` | `⇧⌘Enter` | Reveal the folder in Explorer / Finder |
| `Ctrl+C` | `⌘C` | Copy the folder path |
| `Ctrl+Shift+C` | `⇧⌘C` | Copy the session id |
| `Ctrl+Alt+C` | `⌥⌘C` | Copy the full resume command |
| `Esc` | `Esc` | Hide |

With an empty query it lists the most recent sessions. A session with no recorded folder disables the folder actions and explains why at the bottom of the window.

Resuming opens Windows Terminal (falling back to the command shell) on Windows, and iTerm (falling back to Terminal) on macOS. Both can be overridden in settings.

## Tray / menu-bar menu

`Search…` · `Daily recap…` · `Rebuild index` · `Open settings` · `Open log folder` · `Exit` (`Quit` on macOS).
The tooltip / menu shows the chord that was actually registered, which is not always the configured one.

## Daily recap

**Daily recap…** in the tray / menu-bar menu answers the stand-up question "what did you do yesterday?".

- It opens on your **last active day before today**: Friday on a Monday, your last working day after time off. ◀ ▶ step between days that had activity, up to today.
- The day is shown in full: project → branch → sessions (title and prompt count) → **your git commits** that day. Earlier active days are condensed to one line per project.
- **Copy** puts the Markdown on the clipboard, ready to paste into a chat.
- A day starts at 04:00 by default, so a session that runs past midnight counts toward the day it began. Harness noise (IDE selection notices, slash-command envelopes, interrupted turns) is not counted as work.
- Sessions in a repository's subfolders or worktrees are grouped as one project. Commits are read with your own `git`, filtered by each repository's `user.email`.

**Summarize with Claude** (off by default) asks your own `claude` command line, in print mode with no tools and no saved session, to turn the recap into first-person stand-up bullets. Turning it on in settings is what allows the day's session titles, prompts and commit messages to leave the machine; nothing else in the application does that.

Settings (`Finder:Recap` in the settings file): `DayStartHour` (4), `LookbackDays` (4), `IncludeGit` (true), `EnableAiSummary` (false), `ClaudeExecutable` (blank = search the path and the usual install folders), `SummaryModel` (`haiku`).

## Where everything lives

| What | Windows | macOS |
|---|---|---|
| Settings | `%APPDATA%\ClaudeSessionFinder\settings.json` | `~/Library/Application Support/ClaudeSessionFinder/settings.json` |
| Index | `%LOCALAPPDATA%\ClaudeSessionFinder\index.db` | `~/Library/Application Support/ClaudeSessionFinder/index.db` |
| Logs | `%LOCALAPPDATA%\ClaudeSessionFinder\logs\finder-.log` (rolling, 7 files) | `~/Library/Logs/ClaudeSessionFinder/` (rolling, 7 files) |
| Autostart | `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, value `ClaudeSessionFinder` | `~/Library/LaunchAgents/com.gtama.claudesessionfinder.plist` |

The settings file is the source of truth: autostart is rewritten on every launch to match `Finder:Shell:StartAtLogin`. To turn it off, untick it in the settings window — don't delete the registry value or the launch agent by hand.

**Logs never contain prompt or response text** — only session ids, paths, offsets and counters. Your transcripts are private.

## Rebuilding the index

- From the tray / menu bar: **Rebuild index**. Rereads every transcript from scratch and runs `VACUUM` at the end (the only pass that does; it blocks readers while it runs).
- From the CLI: `finder reindex --full`.
- If the `.db` gets corrupted there is nothing to do: the app detects it on open, deletes it, rebuilds it and keeps running. The event is recorded in the log.

## Diagnostic CLI

```bash
dotnet run --project src/SessionFinder.Cli -- <command>
```

| Command | What it does |
|---|---|
| `parse <file>` | Parses one transcript and prints title, folder, chunks and offset |
| `reindex [--full]` | One pass over every session folder |
| `search "<query>"` | Ranked search against the index |
| `watch` | Follows the files and indexes changes live |
| `status` | Counters, parse errors, db size, last pass |
| `recap [--date yyyy-MM-dd] [--days N] [--no-git] [--prompts] [--summarize]` | Prints the daily recap as Markdown; `--summarize` adds the Claude summary |

## Building from source

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

There is one solution per platform, each containing the shared projects, that platform's UI head and all the tests:

| Solution | Contains |
|---|---|
| `ClaudeSessionFinder.Windows.slnx` | Core, Infrastructure, Presentation, CLI, **WPF head**, tests |
| `ClaudeSessionFinder.Mac.slnx` | Core, Infrastructure, Presentation, CLI, **Avalonia head**, tests |
| `ClaudeSessionFinder.slnx` | Everything (the WPF head compiles on macOS too, but only runs on Windows) |

```bash
dotnet build ClaudeSessionFinder.Mac.slnx      # or ClaudeSessionFinder.Windows.slnx
dotnet test  ClaudeSessionFinder.Mac.slnx
```

Tests tagged `Category=Benchmark` are excluded by default (see `.runsettings`), because they assert on wall-clock times.

## Publishing

**Windows**

```bash
dotnet publish src/SessionFinder.Wpf -c Release -r win-x64 -o <destination>
```

Self-contained, single-file, ReadyToRun: one **~171 MB** `.exe` with no runtime to install. Cold start ~1.8 s (first-run extraction), warm start **~0.3 s** until the hotkey is live. No trimming: the SDK refuses it for WPF/WinForms apps (`NETSDK1175`).

**macOS**

```bash
scripts/build-mac-app.sh            # Release, osx-arm64
```

Produces `artifacts/ClaudeSessionFinder.app`, self-contained and signed ad hoc. The `.app` bundle is required, not cosmetic: it is what keeps the launcher out of the Dock (`LSUIElement`) and what the launch agent opens at login.

## Known limitations

- **Prefix matching only from the start of a token**: `widget` does **not** find `LegacyWidgetFactory`. FTS5 treats the identifier as a single token; fixing that needs a trigram tokenizer, a different schema and a much larger index. Not done.
- **`claude --resume` is strictly relative to the working directory**: it only finds the session if the process runs in the right folder. That is why the app always launches the terminal in the session's folder.
- **The searchable surface is about 2% of the transcript bytes** (prompts and response text, not tool results). This is intentional: virtually every conversation block is still indexed.
- **Tray balloons may be suppressed** by Windows focus settings, so notices also go to the tray icon tooltip and to the log.

## License

[MIT](LICENSE)
