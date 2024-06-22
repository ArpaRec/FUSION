﻿#if DEBUG
using Il2CppSLZ.Marrow.Data;
using Il2CppSLZ.Marrow.Interaction;
using Il2CppSLZ.Marrow.Pool;
using Il2CppSLZ.Marrow.Zones;

using LabFusion.Data;
using LabFusion.Marrow;
using LabFusion.Marrow.Zones;
using LabFusion.Utilities;

using UnityEngine;

namespace LabFusion.Debugging;

public static class DebugZoneMigrator
{
    private static readonly Spawnable _crowbarSpawnable = new()
    {
        crateRef = new("c1534c5a-0c8a-4b82-9f8b-7a9543726f77"),
        policyData = null,
    };
    private static MarrowEntity _migratorEntity = null;

    public static void SpawnMigrator()
    {
        var rigManager = RigData.RigReferences.RigManager;
        var physicsRig = rigManager.physicsRig;

        AssetSpawner.Register(_crowbarSpawnable);

        SafeAssetSpawner.Spawn(_crowbarSpawnable, physicsRig.rightHand.transform.position, physicsRig.rightHand.transform.rotation, (p) =>
        {
            _migratorEntity = p.GetComponent<MarrowEntity>();
        });
    }

    public static void MigrateToZone()
    {
        if (_migratorEntity == null)
        {
            return;
        }

        var rigManager = RigData.RigReferences.RigManager;
        var physicsRig = rigManager.physicsRig;
        var rightHand = physicsRig.rightHand;

        var overlap = Physics.OverlapSphere(rightHand.transform.position, 0.02f, ~0, QueryTriggerInteraction.Collide);

        ZoneCuller foundCuller = null;

        foreach (var collider in overlap)
        {
            var zoneCuller = collider.GetComponent<ZoneCuller>();

            if (zoneCuller == null)
            {
                continue;
            }

            foundCuller = zoneCuller;
            break;
        }

        if (foundCuller == null)
        {
            FusionLogger.Warn("Migration failed, no culler was found.");
            return;
        }

        FusionLogger.Log($"Closest culler was {foundCuller.name}, migrating.");

        ZoneCullHelper.MigrateEntity(foundCuller._zoneId, _migratorEntity);

        // Offset marrow entity to teleport
        var offset = rightHand.transform.position - _migratorEntity.Bodies[0].transform.position;
        _migratorEntity.transform.position += offset;
    }
}
#endif