﻿using LabFusion.Data;

using Il2CppTMPro;

using UnityEngine;

using Il2CppSLZ.Marrow;
using Il2CppSLZ.Marrow.Warehouse;

namespace LabFusion.Utilities
{
    internal static class PersistentAssetCreator
    {
        // ALL FONTS AT THE START:
        // - arlon-medium SDF
        // - nasalization-rg SDF
        private const string _targetFont = "arlon-medium";

        internal static TMP_FontAsset Font { get; private set; }
        internal static HandPose SoftGrabPose { get; private set; }

        private static Action<HandPose> _onSoftGrabLoaded = null;

        internal static void OnLateInitializeMelon()
        {
            CreateTextFont();
        }

        internal static void OnMainSceneInitialized()
        {
            GetHandPose();
        }

        private static void GetHandPose()
        {
            SoftGrabPose = RigData.Refs.RigManager.worldGripHandPose;

            if (SoftGrabPose != null)
            {
                _onSoftGrabLoaded?.Invoke(SoftGrabPose);
            }

            _onSoftGrabLoaded = null;
        }

        public static void HookOnSoftGrabLoaded(Action<HandPose> action)
        {
            if (SoftGrabPose != null)
            {
                action?.Invoke(SoftGrabPose);
            }
            else
            {
                _onSoftGrabLoaded += action;
            }
        }

        private static void CreateTextFont()
        {
            // I don't want to use asset bundles in this mod.
            // Is this a bad method? Sure, but it only runs once.
            // So WHO CARES!
            var fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            foreach (var font in fonts)
            {
                if (font.name.ToLower().Contains(_targetFont))
                {
                    Font = font;
                    break;
                }
            }

            // Make sure we at least have a font
            if (Font == null)
            {
#if DEBUG
                FusionLogger.Error($"Failed finding the {_targetFont} font! Defaulting to the first font in the game!");
#endif

                Font = fonts[0];
            }
        }

        internal static void SetupImpactProperties(RigManager rig)
        {
            var physRig = rig.physicsRig;
            var rigidbodies = physRig.GetComponentsInChildren<Rigidbody>(true);

            var surfaceData = new DataCardReference<SurfaceDataCard>("SLZ.Backlot.SurfaceDataCard.Blood");

            for (var i = 0; i < rigidbodies.Length; i++)
            {
                var rb = rigidbodies[i];
                var go = rb.gameObject;

                // Check if it already has impact properties
                if (rb.GetComponent<ImpactProperties>())
                {
                    continue;
                }

                // Ignore specific rigidbodies
                if (go == physRig.knee || go == physRig.feet)
                {
                    continue;
                }
            
                var properties = go.AddComponent<ImpactProperties>();
                properties.SurfaceDataCard = surfaceData;
                properties.decalType = ImpactProperties.DecalType.None;
            }
        }
    }
}