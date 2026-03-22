# GitHub Actions Setup Guide

## Docker Hub Token Created ✅

You've created a Docker Hub access token:
- Description: `kelliphoto-github-actions`
- Permissions: Read, Write, Delete
- Token: `[YOUR_TOKEN_HERE]` (save this securely - it starts with `dckr_pat_`)

## Next Steps

### 1. Add GitHub Secrets

1. Go to: https://github.com/jedon/kelliphoto/settings/secrets/actions

2. Click **"New repository secret"** and add:

   **Secret 1:**
   - Name: `DOCKERHUB_USERNAME`
   - Value: `jedon`

   **Secret 2:**
   - Name: `DOCKERHUB_TOKEN`
   - Value: `[Paste your Docker Hub token here - it starts with dckr_pat_]`

3. Click **"Add secret"** for each one

### 2. Verify docker-compose.yml

The `docker/docker-compose.yml` has been updated with your Docker Hub username (`jedon`).

### 3. Trigger GitHub Actions Build

After adding the secrets, push a commit to trigger the build:

```bash
git add docker/docker-compose.yml
git commit -m "Update Docker Hub username to jedon"
git push origin main
```

Or create an empty commit to trigger the workflow:

```bash
git commit --allow-empty -m "Trigger GitHub Actions build"
git push origin main
```

### 4. Monitor the Build

1. Go to: https://github.com/jedon/kelliphoto/actions
2. You should see the "Build and Push Docker Image" workflow running
3. Wait for it to complete (usually 5-10 minutes)

### 5. Verify Image on Docker Hub

Once the build completes, check:
- https://hub.docker.com/r/jedon/kelliphoto-web

You should see the image tagged as `latest` and with your branch name.

## Troubleshooting

### Build Fails

- Check GitHub Actions logs: https://github.com/jedon/kelliphoto/actions
- Verify secrets are set correctly
- Check Docker Hub token permissions

### Image Not Appearing

- Wait a few minutes for Docker Hub to sync
- Check the workflow completed successfully
- Verify the image name matches: `jedon/kelliphoto-web`

## Security Note

⚠️ **Important**: The Docker Hub token is sensitive. Never commit it to git or share it publicly. It's stored securely in GitHub Secrets.
