using System.Collections.Generic;
using UnityEngine;

public class HelicopterStoreTrigger : MonoBehaviour
{

    private void OnTriggerEnter(Collider other) {
        if (other.attachedRigidbody != null && other.attachedRigidbody.CompareTag("Player") && !GR.Player.instance.IsDead) {
            HelicopterManager.instance.inHelicopter = true;
        }
    }

    private void OnTriggerExit(Collider other) {
        if (other.attachedRigidbody != null && other.attachedRigidbody.CompareTag("Player") && !GR.Player.instance.IsDead) {
            HelicopterManager.instance.inHelicopter = false;
        }
    }
}
