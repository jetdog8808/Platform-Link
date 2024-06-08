
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class PlatformLink : UdonSharpBehaviour
{
    [Tooltip("Which layers should the player link too.")]
    public LayerMask linkToLayers;
    [Tooltip("Should the player keep the platforms velocity when getting off the platform.")]
    public bool inheriteVelocity = true;
    [Tooltip("How high off the collider should the player be before they unlink from it.")]
    public float unLinkDistance = 10f;

    private Transform linkedObject;
    private Vector3 lastWorldPos, lastLocalPos, lastPlatformPos, lastLocalRot;
    private Quaternion lastWorldRot;
    private float inputH, inputV,
        unLinkTimer;
    private RaycastHit[] hitObjects = new RaycastHit[128];
    private Collider[] localOverlaps = new Collider[32];
    private RaycastHit invalidHit;
    private const int localLayerMask = 1024;
    private int localPlayerCollision;
    private VRCPlayerApi localPlayer;
    
    private void Start()
    {
        localPlayer = Networking.LocalPlayer;
        //creating local player layer collision matrix mask. 
        for (int i = 0; i < 32; i++)
        {
            localPlayerCollision = localPlayerCollision | (Physics.GetIgnoreLayerCollision(10, i) ? 0 : (1 << i));
        }

        invalidHit.distance = float.MaxValue;
    }
    
    public void LateUpdate()
    {
        Vector3 platformVelocity = Vector3.zero;
#if !UNITY_EDITOR
        //VRC Runtime:
        VRCPlayerApi.TrackingData avatarRoot = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.AvatarRoot);
        //check if player is in station.
        if (StationCheck(avatarRoot.position))
        {
            //release from platform if linked.
            if(linkedObject != null)
            {
                ReleaseFromPlatform(Vector3.zero);
            }

            return;
        }

        //If linked move with the platform.
        if (linkedObject != null)
        {
            //last location on platform + how much player moved from last frame.
            Vector3 teleportPoint = linkedObject.TransformPoint(lastLocalPos) + (avatarRoot.position - lastWorldPos);
            //last rotation vector on platform projected onto +y normal + how much player has rotated from last frame.
            Quaternion teleportRot = Quaternion.LookRotation(Vector3.ProjectOnPlane(linkedObject.TransformDirection(lastLocalRot), Vector3.up)) * (avatarRoot.rotation * Quaternion.Inverse(lastWorldRot));
            float velocityZ, velocityX;
            Vector3 currentVelocity = localPlayer.GetVelocity();

            if (localPlayer.IsUserInVR())
            {
                // teleport explanation -> gist.github.com/Phasedragon/5b76edfb8723b6bc4a49cd43adde5d3d
                VRCPlayerApi.TrackingData origin = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Origin);
                Quaternion invPlayerRot = Quaternion.Inverse(avatarRoot.rotation);
                localPlayer.TeleportTo(teleportPoint + teleportRot * invPlayerRot * (origin.position - avatarRoot.position), teleportRot * (invPlayerRot * origin.rotation), VRC_SceneDescriptor.SpawnOrientation.AlignRoomWithSpawnPoint, true);

                velocityZ = localPlayer.GetRunSpeed() * inputV;
                velocityX = localPlayer.GetStrafeSpeed() * inputH;
            }
            else
            {
                localPlayer.TeleportTo(teleportPoint, teleportRot, VRC_SceneDescriptor.SpawnOrientation.AlignPlayerWithSpawnPoint, true);

                if (Input.GetKey(KeyCode.LeftShift))
                {
                    velocityZ = localPlayer.GetRunSpeed() * inputV;
                }
                else
                {
                    velocityZ = localPlayer.GetWalkSpeed() * inputV;
                }

                velocityX = localPlayer.GetStrafeSpeed() * inputH;
            }

            bool grounded = localPlayer.IsPlayerGrounded();
            Quaternion visualRot = localPlayer.GetRotation();
            //rotating wold velocity to local player velocity.
            Vector3 localVelocity = Quaternion.Inverse(visualRot) * currentVelocity;
            //controlling players velocity when grounded to give better control.
            localPlayer.SetVelocity(visualRot * new Vector3((Mathf.Approximately(inputH, 0f) && !grounded) ? localVelocity.x : velocityX, Mathf.Clamp(currentVelocity.y, float.MinValue, localPlayer.GetJumpImpulse()), (Mathf.Approximately(inputV, 0f) && !grounded) ? localVelocity.z : velocityZ));
            
            lastWorldPos = teleportPoint;
            lastLocalPos = linkedObject.InverseTransformPoint(teleportPoint);
            lastWorldRot = teleportRot;
            lastLocalRot = linkedObject.InverseTransformDirection(teleportRot * Vector3.forward);

            platformVelocity = (linkedObject.position - lastPlatformPos) / Time.deltaTime;
            lastPlatformPos = linkedObject.position;
            // syncs up the the menus colliders. making it easier to use on platform.
            Physics.SyncTransforms();
        }

        //Check if there is a valid platfrom and link/unlink from results. 
        if (PlatformCheck(avatarRoot.position, out RaycastHit hitinfo))
        {
            //is player currently linked to a platform.
            if(linkedObject == null)
            {
                //only link if player is grounded
                if (localPlayer.IsPlayerGrounded()) LinkToPlatform(hitinfo.transform, avatarRoot.position, avatarRoot.rotation);
            }
            else
            {
                //reset unlink timer, and link to new platform if different then current platform.
                unLinkTimer = 0f;
                if (linkedObject != hitinfo.transform) LinkToPlatform(hitinfo.transform, avatarRoot.position, avatarRoot.rotation);
            }
        }
        else
        {
            //if currently linked to a platform.
            if (linkedObject != null)
            {
                //add time to unlink timer. If greater then threshold release from platform.
                unLinkTimer += Time.deltaTime;
                if(unLinkTimer > 0.1f)
                {
                    ReleaseFromPlatform(platformVelocity);
                }                
            }
        }
#else
        //clientsim:
        Vector3 avatarPos = localPlayer.GetPosition();
        Quaternion avatarRot = localPlayer.GetRotation();
        
        if (StationCheck(avatarPos)) return;
        
        if (linkedObject != null)
        {
            Vector3 teleportPoint = linkedObject.TransformPoint(lastLocalPos) + (avatarPos - lastWorldPos);
            Quaternion teleportRot = Quaternion.LookRotation(Vector3.ProjectOnPlane(linkedObject.TransformDirection(lastLocalRot), Vector3.up)) * (avatarRot * Quaternion.Inverse(lastWorldRot));

            localPlayer.TeleportTo(teleportPoint, teleportRot, VRC_SceneDescriptor.SpawnOrientation.AlignPlayerWithSpawnPoint, true);

            lastWorldPos = teleportPoint;
            lastLocalPos = linkedObject.InverseTransformPoint(teleportPoint);
            lastWorldRot = teleportRot;
            lastLocalRot = linkedObject.InverseTransformDirection(teleportRot * Vector3.forward);

            platformVelocity = (linkedObject.position - lastPlatformPos) / Time.deltaTime;
            lastPlatformPos = linkedObject.position;
        }

        if (PlatformCheck(avatarPos, out RaycastHit hitinfo))
        {
            if (linkedObject == null)
            {
                if (localPlayer.IsPlayerGrounded()) LinkToPlatform(hitinfo.transform, avatarPos, avatarRot);
            }
            else
            {
                if (linkedObject != hitinfo.transform) LinkToPlatform(hitinfo.transform, avatarPos, avatarRot);
            }
        }
        else
        {
            if (linkedObject != null) ReleaseFromPlatform(platformVelocity);
        }
#endif
    }

    //Check if there are any valid platform below.
    private bool PlatformCheck(Vector3 position, out RaycastHit hitInfo)
    {
        //since normal physics casts dont listen to layeroverrides I get all colliders and filter for what I need.
        int hitCount = Physics.SphereCastNonAlloc(position + new Vector3(0f, 0.23f, 0f), 0.18f, Vector3.down, hitObjects, unLinkDistance, -1, QueryTriggerInteraction.Ignore);
        //loops through results and filters hits, returns closest hit the localplayer collides with.
        hitInfo = invalidHit;
        int hitInfoLayer = 0;
        for (int i = 0; i < hitCount; i++)
        {
            //if hit is lower the the current valid collider or a protected object. can just skip to next in list.
            if ((hitObjects[i].distance > hitInfo.distance) || (hitObjects[i].collider == null)) continue;
            //converts colliders layer number to a layer mask for easier filtering.
            int hitLayerMask = 1 << (hitObjects[i].collider.gameObject.layer);
            //is there no rigidbody.
            if (hitObjects[i].rigidbody == null)
            {
                //is the local player able to collide with this colliders layer.
                if ((localPlayerCollision & hitLayerMask) != 0)
                {
                    //if the local player is excluded then continue.
                    if (((hitObjects[i].collider.excludeLayers) & localLayerMask) != 0) continue;
                }
                else
                {
                    //if local player is not included or excluded continue.
                    if (((hitObjects[i].collider.includeLayers & (~hitObjects[i].collider.excludeLayers)) & localLayerMask) == 0) continue;
                }
            }
            else
            {
                //is the local player able to collide with this colliders layer.
                if ((localPlayerCollision & hitLayerMask) != 0)
                {
                    //if the local player is excluded then continue.
                    if ((((hitObjects[i].collider.excludeLayers) | (hitObjects[i].rigidbody.excludeLayers)) & localLayerMask) != 0) continue;
                }
                else
                {
                    //if local player is not included or excluded continue.
                    if ((((hitObjects[i].collider.includeLayers) | (hitObjects[i].rigidbody.includeLayers)) & (~((hitObjects[i].collider.excludeLayers) | hitObjects[i].rigidbody.excludeLayers)) & localLayerMask) == 0) continue;
                }
            }

            //save valid hitinfo and the layer.
            hitInfo = hitObjects[i];
            hitInfoLayer = hitLayerMask;
        }
        //is a valid platform if hit was valid, and was a layer to link too, and collider has a rigidbody.
        return (hitInfo.colliderInstanceID != 0) && ((hitInfoLayer & linkToLayers) != 0) && (hitInfo.rigidbody != null);
    }

    //checks if a player is in a station.
    private bool StationCheck(Vector3 position)
    {
        //checking local player layer for any protected colliders. if there is the player is not in a station. if there are none then the player is in a station.
        int overlapCount = Physics.OverlapSphereNonAlloc(position, 1f, localOverlaps);
        for(int i = 0; i < overlapCount; i++)
        {
            if (!Utilities.IsValid(localOverlaps[i])) return false;
        }

        return true;
    }

    //setup variables at start of link.
    private void LinkToPlatform(Transform platform, Vector3 pos, Quaternion rot)
    {
        linkedObject = platform;
        lastWorldPos = pos;
        lastLocalPos = platform.InverseTransformPoint(pos);
        lastWorldRot = rot;
        lastLocalRot = platform.InverseTransformDirection(rot * Vector3.forward);
        lastPlatformPos = platform.position;
    }

    //unlinks player from platform.
    private void ReleaseFromPlatform(Vector3 Velocity)
    {
        linkedObject = null;
        unLinkTimer = 0f;
        //if inherit velocity is true add release velocity to player.
        if (inheriteVelocity) localPlayer.SetVelocity(localPlayer.GetVelocity() + Velocity);
    }

    public override void InputMoveHorizontal(float value, UdonInputEventArgs args)
    {
        inputH = value;
    }

    public override void InputMoveVertical(float value, UdonInputEventArgs args)
    {
        inputV = value;
    }

}
