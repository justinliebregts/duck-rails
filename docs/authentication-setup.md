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

Both Photon apps must have Custom authentication configured to `https://duckrails.gunraidersapi.com/api/photon/authenticate`, with the dashboard-only `secret` parameter matching the backend's `PHOTON_AUTH_PHASE`. Do not put that secret in Unity. The inherited local-session shortcut for additional Editor instances has no backend token and cannot be used as evidence of secured multiplayer authentication; use separate authenticated accounts/headsets.

## Verification and remaining work

On October 28, 2026, `go test ./middleware ./config ./handler -count=1` passed, including a new multipart Unity request/session-response test and existing valid/invalid Editor proof checks. `go build` passed. `dotnet build Assembly-CSharp.csproj` passed with 429 warnings and zero errors. These are source-level/local checks; Meta is stubbed in the backend contract test.

The backend is not deployed. DNS/TLS, database/schema/account, credentials, Editor allowlist and both Photon dashboard providers still need setup. Then test fresh/repeat login, rejected credentials, session refresh, Realtime/Voice connections, room switching and reconnect on actual headsets. No milestone acceptance is claimed from compilation.

The Classic backend lacks the Duckz wallet/store/IAP/loadout/admin-check and player-cosmetics endpoints invoked asynchronously after login. Their failures do not gate sign-in. Confirmed October 28: V1 has no coins or wallet, so inherited wallet/coin client requests should be removed rather than implementing matching backend endpoints. Direct platform IAP item transactions are required; integrate applicable existing verification, transaction deduplication, ownership and restoration functionality. Product types remain to be defined. This scope update records the decision; it does not implement those changes.
