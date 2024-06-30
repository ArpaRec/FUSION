﻿using Il2CppSLZ.Marrow.Forklift.Model;
using Il2CppSLZ.Marrow.Warehouse;

using LabFusion.Utilities;

namespace LabFusion.Downloading;

public static class ModForklift
{
    public struct PalletShipment
    {
        public string palletPath;
        public ModListing modListing;
        public Action onFinished;

        public override readonly int GetHashCode()
        {
            return palletPath.GetHashCode();
        }
    }

    private static readonly Queue<PalletShipment> _scheduledShipments = new();

    public static void UpdateForklift()
    {
        if (!AssetWarehouse.ready)
        {
            return;
        }

        if (_scheduledShipments.Count > 0)
        {
            LoadPallet(_scheduledShipments.Dequeue());
        }
    }

    private static void LoadPallet(PalletShipment shipment)
    {
        var palletPath = shipment.palletPath;

#if DEBUG
        FusionLogger.Log($"Loading pallet at path {palletPath}.");
#endif

        var warehouse = AssetWarehouse.Instance;
        var palletTask = warehouse.LoadPalletFromFolderAsync(palletPath, true, null, shipment.modListing);

        var onCompleted = () =>
        {
            // Get pallet from path
            Pallet pallet = null;
            var manifests = AssetWarehouse.Instance.GetPalletManifests();

            foreach (var manifest in manifests)
            {
                if (manifest.PalletPath == palletPath)
                {
                    pallet = manifest.Pallet;
                    break;
                }
            }

            // Send download notification
            if (pallet != null)
            {
                FusionNotifier.Send(new()
                {
                    isPopup = true,
                    showTitleOnPopup = true,
                    title = "Download Completed",
                    type = NotificationType.SUCCESS,
                    popupLength = 4,
                    message = $"Finished installing {pallet.Title}!"
                });
            }

            // Invoke complete callback
            shipment.onFinished?.Invoke();
        };
        palletTask.GetAwaiter().OnCompleted(onCompleted);
    }

    public static void SchedulePalletLoad(PalletShipment shipment)
    {
        if (_scheduledShipments.Contains(shipment))
        {
            return;
        }

        _scheduledShipments.Enqueue(shipment);
    }
}
