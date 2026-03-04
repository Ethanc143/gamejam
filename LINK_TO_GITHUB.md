# Connect This Project to the Original GitHub Repo

You downloaded this as a ZIP, so there’s no git history. To link it back to the original repo and pull updates:

## 1. Find the original repo URL

In your browser, open the GitHub page you downloaded from. The URL looks like:

`https://github.com/Ethanc143/gamejam`

## 2. Open a terminal in this folder

- In Cursor/VS Code: **Terminal → New Terminal** (or `` Ctrl+` ``).
- Or open Command Prompt / PowerShell and `cd` to this project folder.

## 3. Turn this folder into a git repo and add the original as `origin`

Run these:

```bash
git init
git remote add origin https://github.com/Ethanc143/gamejam
```

**origin** = the original repo (no "upstream" — it's just your one main remote).

## 4. Save your current work as the first commit

```bash
git add .
git commit -m "My modifications"
```

## 5. (Optional) Pull updates from the original repo

To get the latest changes from the original repo:

```bash
git fetch origin
git merge origin/main
```

If the original repo uses `master` instead of `main`:

```bash
git merge origin/master
```

Resolve any merge conflicts if they appear.

---

## If you want to push your changes to your own GitHub copy

1. Create a **new repo** on GitHub (e.g. `yourusername/gamejam`).
2. Add it as a second remote (e.g. `myfork`) so **origin** stays the original:

```bash
git remote add myfork https://github.com/YOUR_USERNAME/YOUR_REPO.git
git branch -M main
git push -u myfork main
```

Then you’ll have:

- **origin** = original repo (pull updates)
- **myfork** = your copy (push your changes)
