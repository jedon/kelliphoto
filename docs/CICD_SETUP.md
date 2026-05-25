# CI/CD: push to GitHub → tested → deployed

One workflow (`.github/workflows/ci-cd.yml`) runs on every push/PR to `main` / `master`:

| Job | What it does |
|-----|----------------|
| **unit-tests** | `dotnet restore`, build, **43+ xUnit tests** (integration + unit) |
| **playwright** | Starts the app in `Testing` mode (in-memory DB), runs **Playwright** specs in `e2e/` |
| **build-push** | Builds `docker/Dockerfile`, pushes `jedon/kelliphoto-web` to Docker Hub (push to main only, not PRs) |
| **deploy** | SSH to your server, `docker pull`, recreate `kelliphoto-web` (opt-in, see below) |

## 1. GitHub secrets (Settings → Secrets and variables → Actions)

| Secret | Used for |
|--------|----------|
| `DOCKERHUB_USERNAME` | Docker build/push |
| `DOCKERHUB_TOKEN` | Docker Hub login |
| `CONNECTION_STRINGS__DEFAULT_CONNECTION` | Unit test host configuration (same as local `.env`) |
| `EMAIL__SMTP_PASSWORD` | Unit test host configuration |
| `DEPLOY_HOST` | SSH hostname or IP for deploy job (e.g. `142.4.216.160`) |
| `DEPLOY_USER` | SSH user (e.g. `debian`) |
| `DEPLOY_SSH_KEY` | Private key (full PEM, including `-----BEGIN...`) |
| `DEPLOY_SSH_PORT` | Optional; default `22` |

## 2. Repository variables (Settings → Secrets and variables → Actions → Variables)

| Variable | Value | Purpose |
|----------|-------|---------|
| `ENABLE_DEPLOY` | `true` | Turn on automatic deploy after a green build on `main` |
| `DEPLOY_RUNNER` | `self-hosted` | **Recommended** on Proxmox/private LAN — see below |
| `DEPLOY_COMPOSE_DIR` | `~/kelli.photo/docker` | Optional override |
| `DEPLOY_SCRIPT_PATH` | `~/kelli.photo/scripts/deploy/remote-deploy.sh` | Optional override |

Leave `ENABLE_DEPLOY` unset (or not `true`) until SSH/runner is ready — builds and tests still run; deploy is skipped.

## 3. Server preparation (Debian VM)

On the machine where Docker/Portainer runs:

```bash
# Clone repo so deploy script exists (once)
git clone https://github.com/jedon/kelliphoto.git ~/kelli.photo
# Or sync only scripts/docker if you manage the stack purely in Portainer

# Ensure compose stack exists
cd ~/kelli.photo/docker
cp .env.example .env   # fill CONNECTION_STRINGS__DEFAULT_CONNECTION, etc.
docker compose up -d

# Deploy user: passwordless docker for your SSH user (example)
sudo usermod -aG docker "$USER"
```

Create a **deploy key** for GitHub Actions:

```bash
ssh-keygen -t ed25519 -f ~/.ssh/kelliphoto-deploy -N ""
cat ~/.ssh/kelliphoto-deploy.pub >> ~/.ssh/authorized_keys
# Paste private key into GitHub secret DEPLOY_SSH_KEY
```

## 4. Private network (Proxmox) — self-hosted runner

GitHub’s cloud runners can SSH to your server if `DEPLOY_HOST` is reachable (e.g. `142.4.216.160`). For a private LAN only, use a **self-hosted runner** on the VM (outbound-only to GitHub; no port forward needed).

### Upload deploy scripts manually (if needed)

`Set-SCPItem` / `scp` **cannot** create parent folders. The remote directory must exist first:

```powershell
.\scripts\deploy\Upload-DeployScript.ps1
# or: ssh jedon@142.4.216.160 "mkdir -p ~/kelli.photo/scripts/deploy"
# then scp to jedon@host:~/kelli.photo/scripts/deploy/   (note trailing slash = directory)
```

Normally **git pull on the server** is enough — `scripts/deploy/remote-deploy.sh` is in the repo.

### Option A — from Windows (on the same LAN as the VM)

```powershell
cd G:\Programming\kelli.photo
.\scripts\deploy\install-github-runner-remote.ps1
# prompts for jedon@142.4.216.160 password
```

### Option B — SSH in yourself, then install

On your PC (must reach `142.4.216.160:22`):

```powershell
ssh jedon@142.4.216.160
```

On the **Debian VM**, get a token (from any machine with `gh` auth):

```powershell
gh api repos/jedon/kelliphoto/actions/runners/registration-token --method POST
```

Copy the `token` value, then on Debian:

```bash
git clone https://github.com/jedon/kelliphoto.git ~/kelli.photo   # if not already
cd ~/kelli.photo
export RUNNER_TOKEN='PASTE_TOKEN_HERE'
bash scripts/deploy/install-github-runner.sh
```

Or download only the script:

```bash
curl -fsSL -o install-github-runner.sh \
  https://raw.githubusercontent.com/jedon/kelliphoto/main/scripts/deploy/install-github-runner.sh
chmod +x install-github-runner.sh
export RUNNER_TOKEN='PASTE_TOKEN_HERE'
./install-github-runner.sh
```

Registration tokens expire in about **one hour**.

### Cloud deploy (public host `142.4.216.160`) — recommended here

No self-hosted runner. GitHub Actions SSHs to your server after a green build.

**Secrets:** `DEPLOY_HOST`, `DEPLOY_USER`, `DEPLOY_SSH_KEY` (and existing Docker Hub secrets).

**Variables:** `ENABLE_DEPLOY` = `true`. Do **not** set `DEPLOY_RUNNER` (or leave it empty).

Deploy runs `~/kelli.photo/scripts/deploy/remote-deploy.sh` over SSH.

### Self-hosted runner (private LAN only)

Set `DEPLOY_RUNNER` = `self-hosted` and install a runner on the VM (see above).

## 5. Database migrations

Deploy **does not** run EF migrations automatically (production DB is external). After schema changes:

```bash
cd ~/kelli.photo/src/KelliPhoto.Web
dotnet ef migrations script -o ~/complete-migration.sql
psql -h ... -f ~/complete-migration.sql
docker restart kelliphoto-web
```

See `docs/APPLY_MIGRATIONS_GUIDE.md`.

## 6. Local commands

```bash
# Unit tests
dotnet test

# Playwright (install browsers once)
cd e2e
npm ci
npx playwright install chromium
npm test
```

## 7. Semantic versioning

The app version is defined in the repo root [`VERSION`](../VERSION) file (e.g. `1.0.0`). CI builds Docker images with metadata appended: `1.0.0+abc1234` (short git SHA). Git tags `v1.2.3` produce release version `1.2.3`.

- **Bump** before a release: edit `VERSION` (major/minor/patch per [semver](https://semver.org/)).
- **Verify**: open the login page — `Version …` appears under the sign-in form.
- **Tag a release** (optional): `git tag v1.0.0 && git push origin v1.0.0`

## 8. Typical release flow

1. Push to `main`.
2. Watch **Actions** → all jobs green.
3. Deploy job pulls `jedon/kelliphoto-web:latest` and recreates the container.
4. Nginx on the host continues to proxy `https://kelli.photo` → `127.0.0.1:8888`.

To deploy manually without pushing:

```bash
ssh user@your-server 'bash ~/kelli.photo/scripts/deploy/remote-deploy.sh'
```

## 9. Troubleshooting

| Issue | Fix |
|-------|-----|
| Deploy skipped | Set variable `ENABLE_DEPLOY=true` |
| SSH timeout from Actions | Use self-hosted runner or VPN; cloud runners need a reachable host |
| Playwright fails in CI | Download artifact `playwright-report`; check Blazor/WebSocket errors |
| 502 after deploy | `curl http://127.0.0.1:8888/` on server; check `docker logs kelliphoto-web` |
| Old image still running | `docker compose pull web && docker compose up -d --force-recreate web` |
