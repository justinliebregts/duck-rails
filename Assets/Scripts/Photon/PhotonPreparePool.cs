using UnityEngine;
using Photon.Pun;

public class PhotonPreparePool : MonoBehaviour
{
    [SerializeField] GameObject[] prefabs;

    public static void Register(GameObject prefab)
    {
        if (prefab == null)
            return;

        if (PhotonNetwork.PrefabPool is DefaultPool pool)
            pool.ResourceCache[prefab.name] = prefab;
    }

    void Awake()
    {
        if (prefabs == null)
            return;

        foreach (GameObject prefab in prefabs)
            Register(prefab);
    }
}
