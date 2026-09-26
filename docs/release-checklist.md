# First public release checklist

## Pre-release verification

- [ ] Run a real Windows x64 smoke test: launch, icon rendering, WASAPI capture, COM-port discovery, serial connection, visualizer output, and clean shutdown.
- [ ] Optionally run a clean Linux user-install smoke test with `./packaging/linux/install-user.sh`, then confirm launcher, icon, audio capture, serial access, settings persistence, and uninstall behavior.
- [ ] Capture and review the images in the [screenshot plan](screenshots/README.md).
- [ ] Confirm README and documentation links resolve after screenshots are added.
- [ ] Re-run Release tests, build, and both self-contained packaging paths from a clean checkout.

## Create the public repository

- [ ] Create an empty GitHub repository without generated README, license, or `.gitignore` files.
- [ ] Add the new repository as the Git remote.
- [ ] Push `main` and verify the CI workflow completes successfully.
- [ ] Review repository description, topics, default branch, and GPL-3.0-or-later license detection.

## Build and publish the first release

- [ ] Confirm the intended version matches the application metadata and choose the corresponding `v*` tag.
- [ ] Push the version tag and verify both tagged artifact jobs succeed.
- [ ] Download and inspect `LED-Cube-Studio-linux-x64.tar.gz` and `LED-Cube-Studio-win-x64.zip` from the workflow run.
- [ ] Verify each archive contains the executable, `LICENSE.txt`, `THIRD-PARTY-NOTICES.md`, and only the appropriate shared/platform license files.
- [ ] If desired, manually create a GitHub Release and attach the two archives produced by the tagged workflow.
- [ ] Test the final downloadable archives and recheck README, screenshot, license, and notice links on GitHub.
