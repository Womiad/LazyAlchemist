using UnityEngine;

public class HandItemMount : MonoBehaviour
{
    [System.Serializable]
    private class HandSlot
    {
        public Transform item;
        public Vector3 localPosition;
        public Vector3 localEulerAngles;
        public Vector3 localScale = Vector3.one;
        public bool disablePhysicsWhileHeld = true;
    }

    [SerializeField] private Animator animator;
    [SerializeField] private HandSlot leftHand = new HandSlot();
    [SerializeField] private HandSlot rightHand = new HandSlot();

    private const string LeftAnchorName = "LeftHandAnchor";
    private const string RightAnchorName = "RightHandAnchor";

    private void Reset()
    {
        animator = GetComponentInChildren<Animator>();
    }

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
    }

    private void Start()
    {
        MountItems();
    }

    [ContextMenu("Mount Items Now")]
    private void MountItems()
    {
        MountItem(leftHand, HumanBodyBones.LeftHand, LeftAnchorName, "mixamorig:LeftHand");
        MountItem(rightHand, HumanBodyBones.RightHand, RightAnchorName, "mixamorig:RightHand");
    }

    private void MountItem(HandSlot slot, HumanBodyBones bone, string anchorName, string fallbackBoneName)
    {
        if (slot == null || slot.item == null)
        {
            return;
        }

        var hand = FindHandBone(bone, fallbackBoneName);
        if (hand == null)
        {
            Debug.LogWarning($"HandItemMount could not find {bone} on {name}.", this);
            return;
        }

        var anchor = FindOrCreateAnchor(hand, anchorName);
        slot.item.SetParent(anchor, false);
        slot.item.localPosition = slot.localPosition;
        slot.item.localRotation = Quaternion.Euler(slot.localEulerAngles);
        slot.item.localScale = slot.localScale;

        if (slot.disablePhysicsWhileHeld)
        {
            DisablePhysics(slot.item);
        }
    }

    private Transform FindHandBone(HumanBodyBones bone, string fallbackBoneName)
    {
        if (animator != null && animator.isHuman)
        {
            var humanBone = animator.GetBoneTransform(bone);
            if (humanBone != null)
            {
                return humanBone;
            }
        }

        return FindChildByName(transform, fallbackBoneName);
    }

    private static Transform FindOrCreateAnchor(Transform hand, string anchorName)
    {
        var anchor = hand.Find(anchorName);
        if (anchor != null)
        {
            return anchor;
        }

        var anchorObject = new GameObject(anchorName);
        anchor = anchorObject.transform;
        anchor.SetParent(hand, false);
        anchor.localPosition = Vector3.zero;
        anchor.localRotation = Quaternion.identity;
        anchor.localScale = Vector3.one;
        return anchor;
    }

    private static void DisablePhysics(Transform root)
    {
        foreach (var body in root.GetComponentsInChildren<Rigidbody>())
        {
            body.isKinematic = true;
            body.detectCollisions = false;
        }

        foreach (var collider in root.GetComponentsInChildren<Collider>())
        {
            collider.enabled = false;
        }
    }

    private static Transform FindChildByName(Transform parent, string objectName)
    {
        if (parent.name == objectName)
        {
            return parent;
        }

        for (var i = 0; i < parent.childCount; i++)
        {
            var match = FindChildByName(parent.GetChild(i), objectName);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }
}
