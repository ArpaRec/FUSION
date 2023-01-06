﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LabFusion.Network;
using LabFusion.Representation;
using LabFusion.Utilities;
using LabFusion.Syncables;

using SLZ;
using SLZ.Interaction;

using UnityEngine;

using LabFusion.Grabbables;
using LabFusion.Extensions;

namespace LabFusion.Data
{
    public class PropGrabGroupHandler : GrabGroupHandler<SerializedPropGrab>
    {
        public override GrabGroup? Group => GrabGroup.PROP;
    }

    public class SerializedPropGrab : SerializedGrab {
        public string fullPath;
        public ushort index;
        public ushort id;
        public bool isGrabbed;

        public SerializedTransform relativeGrip = null;

        public SerializedPropGrab() { }

        public SerializedPropGrab(string fullPath, ushort index, ushort id, bool isGrabbed, GripPair pair)
        {
            this.fullPath = fullPath;
            this.index = index;
            this.id = id;
            this.isGrabbed = isGrabbed;

            var handTransform = pair.hand.transform;
            var gripTransform = pair.grip.Host.GetTransform();

            relativeGrip = new SerializedTransform(handTransform.InverseTransformPoint(gripTransform.position), handTransform.InverseTransformRotation(gripTransform.rotation));
        }

        public override void Serialize(FusionWriter writer)
        {
            writer.Write(fullPath);
            writer.Write(index);
            writer.Write(id);
            writer.Write(isGrabbed);
            writer.Write(relativeGrip);
        }

        public override void Deserialize(FusionReader reader)
        {
            fullPath = reader.ReadString();
            index = reader.ReadUInt16();
            id = reader.ReadUInt16();
            isGrabbed = reader.ReadBoolean();
            relativeGrip = reader.ReadFusionSerializable<SerializedTransform>();
        }

        public Grip GetGrip(out PropSyncable syncable) {
            GameObject go;
            InteractableHost host;
            syncable = null;

#if DEBUG
            FusionLogger.Log($"Received prop grip request, id was {id}, valid path is {fullPath != "_"}.");
#endif

            if (SyncManager.TryGetSyncable(id, out var foundSyncable))
            {

                if (foundSyncable is PropSyncable prop) {
                    syncable = prop;
#if DEBUG
                    FusionLogger.Log($"Found existing prop grip!");
#endif

                    return syncable.GetGrip(index);
                }
            }
            else if (fullPath != "_" && (go = GameObjectUtilities.GetGameObject(fullPath)) && (host = InteractableHost.Cache.Get(go)))
            {
                syncable = new PropSyncable(host);
                SyncManager.RegisterSyncable(syncable, id);

#if DEBUG
                FusionLogger.Log($"Creating new prop grip with id {id} at index {index}!");
#endif

                return syncable.GetGrip(index);
            }
            else
            {
#if DEBUG
                FusionLogger.Log($"Failed to find prop grip!");
#endif
            }

            return null;
        }

        public override Grip GetGrip() {
            return GetGrip(out _);
        }

        public override void RequestGrab(PlayerRep rep, Handedness handedness, Grip grip, bool useCustomJoint = true) {
            if (isGrabbed) {
                // Set the host position so that the grip is created in the right spot
                var host = grip.Host.GetTransform();
                Vector3 position = host.position;
                Quaternion rotation = host.rotation;

                if (relativeGrip != null) {
                    var hand = rep.RigReferences.GetHand(handedness).transform;

                    host.SetPositionAndRotation(hand.TransformPoint(relativeGrip.position), hand.TransformRotation(relativeGrip.rotation.Expand()));
                }

                // Apply the grab
                base.RequestGrab(rep, handedness, grip, useCustomJoint);

                // Reset the host position
                host.position = position;
                host.rotation = rotation;
            }
        }
    }
}
