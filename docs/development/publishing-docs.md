# Publishing Docs

DaCollector documentation is written in Markdown under `docs/` and rendered with MkDocs Material.

## Hosted URL

When GitHub Pages is enabled for the repository, the docs site is published at:

```text
https://iranman.github.io/DaCollector/
```

## GitHub Pages Setup

In the GitHub repository settings:

1. Open **Settings**.
2. Open **Pages**.
3. Set **Build and deployment** source to **GitHub Actions**.
4. Save the setting.

The `Publish Docs to GitHub Pages` workflow builds on pull requests and deploys on pushes to `main`.

## Local Preview

Install MkDocs Material:

```powershell
pip install mkdocs-material
```

Run the local docs server from the repository root:

```powershell
mkdocs serve
```

Open:

```text
http://127.0.0.1:8000
```

## Strict Build

Before pushing documentation changes, run:

```powershell
mkdocs build --strict
```

This catches broken internal links, invalid navigation entries, and Markdown issues that MkDocs can detect.
