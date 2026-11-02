using System;
using System.Reflection;
using UnityEngine;
using Object=UnityEngine.Object;

// Shared fixture; it exercises the production controller with recording absent.
internal sealed class MovementTestRig : IDisposable
{
    public readonly GameObject root;
    public readonly NewPlayer p;
    public readonly Action<float> move;
    public MovementTestRig() {
        root=new GameObject("Movement regression rig"); root.SetActive(false); p=root.AddComponent<NewPlayer>(); p.enabled=false;
        p.rb=root.AddComponent<Rigidbody>(); p.rb.useGravity=false; p.rb.constraints=RigidbodyConstraints.FreezeRotation;
        p.trackingSpace=Child("Tracking",root.transform,Vector3.zero);
        p.headCollider=Child("Head",p.trackingSpace,Vector3.up*1.6f).gameObject.AddComponent<SphereCollider>();
        p.headCollider.radius=.15f; p.headCollider.enabled=false; p.headEyeOffset=p.headCollider.transform;
        p.bodyCollider=Child("Body",p.trackingSpace,Vector3.up*.8f).gameObject.AddComponent<CapsuleCollider>();
        p.bodyCollider.radius=.15f; p.bodyCollider.height=.5f; p.bodyCollider.enabled=false; p.bodyMidpoint=p.bodyCollider.transform;
        p.leftHandTransform=Child("Left",p.trackingSpace,new Vector3(-.3f,1,0));
        p.rightHandTransform=Child("Right",p.trackingSpace,new Vector3(.3f,1,0));
        p.controllerInput=root.AddComponent<ControllerInput>(); p.controllerInput.enabled=false;
        p.velocityHistorySize=8; p.velocityLimit=.4f; p.maxJumpSpeed=6.5f; p.jumpMultiplier=1.1f; p.maxArmLength=3;
        Set(p,"headBodyOffset",Vector3.down*.8f); Set(p,"bodyHalfHeight",.4f);
        p.InitializeValues(); Set(p,"lastBodyMidpoint",p.bodyMidpoint.position);
        Set(p,"leftHandNotCollidingTime",-100f); Set(p,"rightHandNotCollidingTime",-100f);
        move=(Action<float>)Delegate.CreateDelegate(typeof(Action<float>),p,typeof(NewPlayer).GetMethod("Movement",Private));
        root.SetActive(true); Physics.SyncTransforms();
    }
    public void Dispose() => Object.DestroyImmediate(root);
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Set(NewPlayer p,string field,object value) => typeof(NewPlayer).GetField(field,Private).SetValue(p,value);
    static Transform Child(string name,Transform parent,Vector3 position)
    {
        var child=new GameObject(name).transform;child.SetParent(parent,false);child.localPosition=position;return child;
    }
}
