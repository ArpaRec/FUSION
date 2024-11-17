﻿using System.Collections;

using LabFusion.Downloading;
using LabFusion.Downloading.ModIO;
using LabFusion.Network;
using LabFusion.Player;
using LabFusion.Preferences.Client;
using LabFusion.Utilities;

using MelonLoader;

namespace LabFusion.RPC;

public static class NetworkModRequester
{
    public struct ModCallbackInfo
    {
        public ModIOFile modFile;
        public bool hasFile;
    }

    public struct ModRequestInfo
    {
        public byte target;

        public string barcode;

        public Action<ModCallbackInfo> modCallback;
    }

    public struct ModInstallInfo
    {
        public byte target;

        public string barcode;

        public Action<ModCallbackInfo> beginDownloadCallback;

        public DownloadCallback finishDownloadCallback;

        public long? maxBytes;

        public IProgress<float> reporter;
    }

    private static uint _lastTrackedRequest = 0;

    private static readonly Dictionary<uint, Action<ModCallbackInfo>> _callbackQueue = new();

    public static void OnResponseReceived(uint trackerId, ModCallbackInfo info)
    {
        if (_callbackQueue.TryGetValue(trackerId, out var callback))
        {
            callback(info);
            _callbackQueue.Remove(trackerId);
        }
    }

    public static void RequestAndInstallMod(ModInstallInfo installInfo)
    {
        if (ModDownloadBlacklist.IsBlacklisted(installInfo.barcode))
        {
            installInfo.finishDownloadCallback?.Invoke(DownloadCallbackInfo.FailedCallback);
            return;
        }

        MelonCoroutines.Start(WaitAndInstallMod(installInfo));
    }

    private static IEnumerator WaitAndInstallMod(ModInstallInfo installInfo)
    {
        float elapsed = 0f;
        bool receivedCallback = false;

        RequestMod(new ModRequestInfo()
        {
            target = installInfo.target,
            barcode = installInfo.barcode,
            modCallback = OnModInfoReceived,
        });

        // Wait for timeout
        while (!receivedCallback && elapsed < 5f)
        {
            elapsed += TimeUtilities.DeltaTime;
            yield return null;
        }

        // No callback means this request timed out
        if (!receivedCallback)
        {
            installInfo.finishDownloadCallback?.Invoke(DownloadCallbackInfo.FailedCallback);

            // Remove the callbacks incase it gets received very late
            installInfo.beginDownloadCallback = null;
            installInfo.finishDownloadCallback = null;
        }

        void OnModInfoReceived(ModCallbackInfo info)
        {
            receivedCallback = true;

            if (!info.hasFile)
            {
                installInfo.finishDownloadCallback?.Invoke(DownloadCallbackInfo.FailedCallback);
                return;
            }

            installInfo.beginDownloadCallback?.Invoke(info);

            bool temporary = !ClientSettings.Downloading.KeepDownloadedMods.Value;

            ModIODownloader.EnqueueDownload(new ModTransaction()
            {
                ModFile = info.modFile,
                Temporary = temporary,
                Callback = installInfo.finishDownloadCallback,
                MaxBytes = installInfo.maxBytes,
                Reporter = installInfo.reporter,
            });
        }
    }

    public static void RequestMod(ModRequestInfo info)
    {
        uint trackerId = _lastTrackedRequest++;

        if (info.modCallback != null)
        {
            _callbackQueue.Add(trackerId, info.modCallback);
        }

        // Send the request to the server
        using var writer = FusionWriter.Create(ModInfoRequestData.Size);
        var data = ModInfoRequestData.Create(PlayerIdManager.LocalSmallId, info.target, info.barcode, trackerId);
        writer.Write(data);

        using var request = FusionMessage.Create(NativeMessageTag.ModInfoRequest, writer);
        MessageSender.SendToServer(NetworkChannel.Reliable, request);
    }
}