# Duck Rails authentication setup

The Unity project is the Duckz-derived copy in `X:\Work\duck-rails`. Its independent backend is `X:\Work\duck-rails-web`, copied from Gun Raiders Classic's simplified Go/Fiber backend. Read that repository's `DUCKRAILS-SETUP.md` for provisioning and first-deployment instructions. Source Duckz and Classic projects were not modified.

## Confirmed configuration

- API base: `https://duckrails.gunraidersapi.com/api/v1/`, in both `Assets/Resources/GameConfig.asset` and the `GameConfig.cs` default.
- Meta app: `28912202281742343`.
- Photon Realtime: `36bef5d2-63ad-4567-948e-52e966d7f2e9`.
- Photon Voice: `b7c80e26-6a61-45ca-97d9-478af736a6d3`.

Justin confirmed these app IDs; the Unity assets already matched. Backend build settings now use the same Meta app. Database `duckrails_tencent_global`, account `duckrailsuser`, Redis namespace `duckrails`, and TKE namespace `duckrails` are proposed provisioning names configured in the copy, not verified cloud resources.

## Sign-in contract

`MetaQuestAuth` obtains the app-scoped ID, organization-scoped ID and Meta nonce. `GameSessionClient` sends multipart fields to `player/v2/social-account/oculus`. Quest supplies an attestation token in the Authorization Bearer header. Editor uses `X-Meta-Editor-Login: 1`; the backend verifies the nonce and organization identity and checks `META_EDITOR_USER_IDS`. The Editor no longer sends a shared bypass phrase or substitutes a fake nonce when Meta proof acquisition fails.

Registration returns the existing access token, refresh token, `gr_id`, separate player ID, display name and platform fields. `DuckzApiClient` refreshes through `POST session/{session_id}`. The backend's `-1` session ID is compatible: that handler does not use the path ID.

`PhotonSession` uses PUN over Realtime and supplies Custom authentication parameters `usr`, `pla`, and `tkn`. It preserves Photon's issued authentication object during room transitions and uses the backend's composed `gr_id` identity. `PhotonVoiceChat` copies the PUN authentication settings.

Both Photon apps must have Custom authentication configured to `https://duckrails.gunraidersapi.com/api/photon/authenticate`, with the dashboard-only `secret` parameter matching the backend's `PHOTON_AUTH_PHASE`. Do not put that secret in Unity. DesktopPlayer instances, including additional Editors, use real authentication. Use separate authenticated accounts for concurrent clients; the inherited additional-editor local-session shortcut has been removed.

## Multiplayer Play Mode accounts

For solo desktop iteration, use Default launch with Compound open and Desktop mode selected. Play signs in directly in Compound, displaying progress in its session banner; no Login scene transition is needed. See `week1-session.md` for measured startup evidence. Multiplayer Play Mode retains its configured scene startup below.

Ported the tagged Meta test-user setup from Gun Raiders Classic commits `981f7aaa7eebfa96fac13b072d45500d32a20884` and `bc3c49c349a76183582a00a277bf64f1781436ff`. The source project was read only.

1. Keep the main editor's account in **Meta > Platform > Edit Settings**, with standalone test-user authentication enabled.
2. Open **Duck Rails > Multiplayer Play Mode Users** in the main editor. Enter `Player2`, the second Meta test user's email/password, then choose **Sign in and save**.
3. Use the existing **Two Ducks** scenario in **Window > Play Mode > Scenarios**. Its second editor has the `Player2` tag, and both editors start in Login. With **Duck Rails > Two Ducks Quick Start** checked (the default), both join the same test expedition automatically after authentication. Uncheck it before Play to test normal compound boarding. Keep the main editor's `RailsQuickStart` tag and the second editor's `Player2` tag.
4. Restart additional editors after saving/changing accounts. The tag must exactly match, including case. Each concurrent player must use a different account.

The window saves access tokens and email in local, project-scoped EditorPrefs, not tracked assets; it does not save the password. Virtual editors resolve the main project's preference key, then apply the selected account before Meta SDK initialization. Main-editor credentials are left alone. Missing or ambiguous account tags produce actionable errors rather than selecting an arbitrary or fake user. Credentials saved in the Classic project are not imported automatically.

Quick-start routing follow-up: the main editor's `RailsQuickStart` tag now gates both clients. Setting it to None keeps both in normal compound matchmaking while Player2 retains its account tag. When enabled, quick start uses fixed room `11111`, ignoring the former cached dynamic room. The menu toggle still disables quick start for both clients.

The second account must also be entitled to the Duck Rails Meta app and permitted by the backend's `META_EDITOR_USER_IDS`. A successful Meta sign-in followed by a backend rejection is a separate configuration issue; this change does not bypass either check. The Four Ducks scenario retains its existing LobbyTest behavior and needs distinct saved account tags before authenticated four-client use.

## Verification and remaining work

On October 28, 2026, `go test ./middleware ./config ./handler -count=1` passed, including a new multipart Unity request/session-response test and existing valid/invalid Editor proof checks. `go build` passed. `dotnet build Assembly-CSharp.csproj` passed with 429 warnings and zero errors. These are source-level/local checks; Meta is stubbed in the backend contract test.

Justin confirmed backend deployment and Photon authentication setup completed on October 28. The Week 1 implementation subsequently exercised real Meta/backend sign-in and authenticated Photon room entry with DesktopPlayer. Rejected credentials, session refresh, multiple concurrent accounts and headset behavior still require their own evidence. See `week1-session.md`; no multi-client acceptance is claimed from compilation or one desktop client.

The Classic backend lacks the Duckz wallet/store/IAP/loadout/admin-check and player-cosmetics endpoints invoked asynchronously after login. Their failures do not gate sign-in. Confirmed October 28: V1 has no coins or wallet, so inherited wallet/coin client requests should be removed rather than implementing matching backend endpoints. Direct platform IAP item transactions are required; integrate applicable existing verification, transaction deduplication, ownership and restoration functionality. Product types remain to be defined. This scope update records the decision; it does not implement those changes.
