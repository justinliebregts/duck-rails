using System;
using System.Collections;
using UnityEngine;

public class PlayerSession : MonoBehaviour
{
    public static PlayerSession Instance { get; private set; }

    public string PlayerId { get; private set; }
    public string DisplayName { get; private set; }
    public string AccessToken { get; private set; }
    public string RefreshToken { get; private set; }
    public int SessionId { get; private set; }
    public string GrId { get; private set; }
    public string Platform { get; private set; }
    public bool IsAdmin { get; private set; }
    public string BadgeId { get; private set; } = "";
    public string NametagId { get; private set; } = "";

    public event Action CosmeticsChanged;

    public static PlayerSession Create(GameSessionClient.SessionData session, string fallbackPlayerId, string fallbackName)
    {
        if (Instance != null)
        {
            var previous = Instance;
            // Destroy is deferred. Release the singleton before the replacement's
            // Awake runs, or it will reject the new session as a duplicate.
            Instance = null;
            previous.gameObject.SetActive(false);
            Destroy(previous.gameObject);
        }

        var gameObject = new GameObject(nameof(PlayerSession));
        var playerSession = gameObject.AddComponent<PlayerSession>();
        playerSession.PlayerId = string.IsNullOrEmpty(session.player_id_separated) ? fallbackPlayerId : session.player_id_separated;
        playerSession.DisplayName = string.IsNullOrEmpty(session.display_name) ? fallbackName : session.display_name;
        playerSession.AccessToken = session.access_token;
        playerSession.RefreshToken = session.refresh_token;
        playerSession.SessionId = session.session_id;
        playerSession.GrId = session.gr_id;
        playerSession.Platform = string.IsNullOrEmpty(session.platform) ? GameSessionClient.Platform : session.platform;
        return playerSession;
    }

    public static PlayerSession CreateLocal(string playerId, string displayName)
    {
        return Create(new GameSessionClient.SessionData
        {
            player_id_separated = playerId,
            display_name = displayName,
            platform = "EDITOR",
        }, playerId, displayName);
    }

    public bool TryGetAccessToken(out string accessToken)
    {
        accessToken = AccessToken;
        return !string.IsNullOrEmpty(accessToken);
    }

    public void UpdateTokens(string accessToken, string refreshToken)
    {
        if (!string.IsNullOrEmpty(accessToken))
            AccessToken = accessToken;
        if (!string.IsNullOrEmpty(refreshToken))
            RefreshToken = refreshToken;
    }

    public void RefreshAdmin()
    {
        StartCoroutine(RefreshAdminRoutine());
    }

    public void RefreshCosmetics()
    {
        StartCoroutine(RefreshCosmeticsRoutine());
    }

    IEnumerator RefreshAdminRoutine()
    {
        DuckzApiClient.Response<object> response = null;
        yield return DuckzApiClient.Get<object>("duckz/admin/check", r => response = r);
        IsAdmin = response.Success;
    }

    [Serializable]
    class CosmeticList
    {
        public string equipped;
    }

    IEnumerator RefreshCosmeticsRoutine()
    {
        string badge = "";
        string nametag = "";

        DuckzApiClient.Response<CosmeticList> badgeResponse = null;
        yield return DuckzApiClient.Get<CosmeticList>("player/cosmetics/badge", r => badgeResponse = r);
        if (badgeResponse != null && badgeResponse.Success && badgeResponse.Data != null && badgeResponse.Data.equipped != null)
            badge = badgeResponse.Data.equipped;

        DuckzApiClient.Response<CosmeticList> nametagResponse = null;
        yield return DuckzApiClient.Get<CosmeticList>("player/cosmetics/nametag", r => nametagResponse = r);
        if (nametagResponse != null && nametagResponse.Success && nametagResponse.Data != null && nametagResponse.Data.equipped != null)
            nametag = nametagResponse.Data.equipped;

        BadgeId = badge;
        NametagId = nametag;
        PlayerProps.PushCosmetics();
        CosmeticsChanged?.Invoke();
    }

    public string SigningId => string.IsNullOrEmpty(GrId) ? PlayerId : GrId;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
