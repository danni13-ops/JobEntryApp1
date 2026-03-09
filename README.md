# JobEntryApp1

A .NET Razor Pages web application for managing job entries and task dashboards.

---

## VS Code Setup — multiple Microsoft / GitHub accounts

If you have both a **personal** and a **work** Microsoft account and VS Code keeps authenticating with the wrong one, follow the steps below.

### Why this happens

VS Code has a built-in GitHub authentication provider that silently picks whichever Microsoft account you signed into the editor with first. When that happens to be your personal account, all Git operations (fetch, push, pull) and the GitHub PR extension will target the wrong identity.

### Quick fix

1. **Sign out of the conflicting account in VS Code**
   `Ctrl/Cmd + Shift + P` → *Accounts: Sign Out of GitHub* → choose the personal account.

2. **Sign back in with your work account**
   `Ctrl/Cmd + Shift + P` → *Accounts: Add Account* (or the *Sign In* button in the Accounts menu at the bottom-left) → choose *Sign in with GitHub* → authenticate through your work Microsoft identity.

3. **Verify the active account**
   `Ctrl/Cmd + Shift + P` → *Accounts: View All Accounts* — your work account should be listed as active.

### Workspace-level setting (already applied)

The `.vscode/settings.json` in this repo sets:

```json
"github.gitAuthentication": false
```

This tells VS Code **not** to intercept `git` credential requests. Git will instead use its own credential manager (Git Credential Manager on Windows/macOS, or your configured SSH key) so you stay in control of which account is used for each repository.

If you want VS Code's GitHub extension to manage credentials again (e.g. for the PR panel), set this back to `true` **after** signing in with your work account only.

### SSH alternative (recommended for work accounts)

Using SSH keys avoids HTTPS credential confusion entirely:

```bash
# 1. Generate a key specifically for your work GitHub account
ssh-keygen -t ed25519 -C "you@yourcompany.com" -f ~/.ssh/id_ed25519_work

# 2. Add it to your SSH agent
ssh-add ~/.ssh/id_ed25519_work

# 3. Add the public key to GitHub (work account)
#    → GitHub → Settings → SSH and GPG keys → New SSH key
cat ~/.ssh/id_ed25519_work.pub

# 4. Update this repo's remote to use SSH
git remote set-url origin git@github.com:danni13-ops/JobEntryApp1.git
```

Add an `~/.ssh/config` entry if you need both personal and work keys active at the same time:

```sshconfig
# Work GitHub account
Host github-work
    HostName github.com
    User git
    IdentityFile ~/.ssh/id_ed25519_work

# Personal GitHub account (default)
Host github.com
    HostName github.com
    User git
    IdentityFile ~/.ssh/id_ed25519
```

Then set the remote URL to use the `github-work` alias:

```bash
git remote set-url origin git@github-work:danni13-ops/JobEntryApp1.git
```

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- SQL Server (local or remote) — connection string configured in `appsettings.json`

## Running locally

```bash
dotnet run
```
