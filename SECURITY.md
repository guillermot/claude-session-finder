# Security policy

## Reporting a vulnerability

Please **don't open a public issue** for security problems. Report them privately through GitHub instead:

1. Go to the repository's **Security** tab.
2. Click **Report a vulnerability**.

Include what you found, the steps to reproduce it, and the OS (Windows or macOS) and app version you used. You'll get a reply within a week. Once a fix is released, you'll be credited in the advisory unless you'd rather not be.

## Supported versions

Only the latest release gets security fixes.

## What's in scope

Claude Session Finder reads Claude Code transcripts from `~/.claude/projects`, stores a local search index, and launches other programs (VS Code, `claude --resume`, Explorer/Finder) in folders taken from those transcripts. Examples of issues worth reporting:

- A crafted transcript or folder path that makes the app run unintended commands.
- Session contents leaking outside your machine, or to other users on the same machine.
- The index or settings files being written somewhere unsafe, or with overly broad permissions.
