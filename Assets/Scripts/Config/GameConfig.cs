using UnityEngine;

[CreateAssetMenu(fileName = "GameConfig", menuName = "DuckZ/Game Config")]
public class GameConfig : ScriptableObject
{
    public const string ResourceName = "GameConfig";

    static GameConfig current;

    public static GameConfig Current
    {
        get
        {
            if (current == null)
                current = Resources.Load<GameConfig>(ResourceName);
            return current;
        }
    }

    public string backendBaseUrl = "https://duckrails.gunraidersapi.com/api/v1/";

    [Tooltip("Must match NONCE_SECRET on the Duckz backend deployment. Signs X-Signature for coin/store/IAP requests.")]
    public string requestSigningSecret = "Rpbmcgb24gdGhlIHNob3V";

    [Tooltip("Static store contents. Coin prices are replaced by GET /store, which reads duckz_economy.json.")]
    public StoreCatalog storeCatalog;
}
