﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using QFramework;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.AddressableAssets.ResourceLocators;
using System.Linq;
using System;

public class AddressableInfo {
    public static string BaseUrl;
}

public interface IAddressableSystem : ISystem {
    void SetCallBack(Action<long> OnCheckCompleteNeedUpdate = null, Action OnCompleteDownload = null, Action OnCheckCompleteNoUpdate = null, Action<float, float> OnUpdate = null);
    void GetDownloadAssets();
    void PraseData();
    void DownloadAsset();
    void GetAvatarResource(int aid, Action<AsyncOperationHandle<Sprite>> OnLoad);
    void GetEquipResource(string rid, Action<AsyncOperationHandle<GameObject>> OnLoaded);
}

public class AddressableSystem : AbstractSystem, IAddressableSystem {
    private int downloadFailedTimes = 0;
    private static List<object> downloadKeys;
    private long totalDownloadSize;
    private Action<float, float> OnUpdate;
    private Action<long> OnCheckCompleteUpdate;
    private Action OnCheckCompleteNoUpdate;
    private Action OnCompleteDownload;
    private AsyncOperationHandle downloadHandle;

    public void PraseData() {
        AddressableInfo.BaseUrl = this.GetSystem<INetworkSystem>().HttpBaseUrl;
    }

    public void SetCallBack(Action<long> OnCheckCompleteNeedUpdate = null, Action OnCompleteDownload = null, Action OnCheckCompleteNoUpdate = null, Action<float, float> OnUpdate = null) {
        this.OnCheckCompleteUpdate = OnCheckCompleteNeedUpdate ?? OnCheckCompleteUpdate;
        this.OnCompleteDownload = OnCompleteDownload ?? this.OnCompleteDownload;
        this.OnCheckCompleteNoUpdate = OnCheckCompleteNoUpdate;
        this.OnUpdate = OnUpdate ?? this.OnUpdate;
    }

    public void GetDownloadAssets() {
        CoroutineController.manager.StartCoroutine(GetTotalDonwloadKeys());
    }

    IEnumerator GetTotalDonwloadKeys() {
        var initHandle = Addressables.InitializeAsync();
        initHandle.Completed += OnInitAA;
        yield return initHandle;
    }

    void OnInitAA(AsyncOperationHandle<IResourceLocator> handle) {
        string str = "";
        for (int i = 0; i < handle.Result.Keys.ToList().Count; i++) {
            str += handle.Result.Keys.ToList()[i] + "; ";
        }
        Debug.LogError("++++++ OnInitFinish : " + str);
        downloadKeys = handle.Result.Keys.ToList();

        var checkHandle = Addressables.CheckForCatalogUpdates(false);
        checkHandle.Completed += OnCheckCatalogUpdate;
        Addressables.Release(handle);
    }

    void OnCheckCatalogUpdate(AsyncOperationHandle<List<string>> handle) {
        string str = "";
        for (int i = 0; i < handle.Result.ToList().Count; i++) {
            str += handle.Result.ToList()[i] + "; ";
        }
        Debug.LogError("对比Catalog " + str);
        if (handle.Result.Count > 0) {
            Debug.LogError("对比Catalog 数量大于0");
            var updateHandle = Addressables.UpdateCatalogs(handle.Result);
            updateHandle.Completed += OnUpdateCatalogs;
        } else {
            Debug.LogError("对比Catalog GetDownloadSizeAsync");
            Addressables.GetDownloadSizeAsync(downloadKeys).Completed += OnCheckDownload;
        }
        Addressables.Release(handle);
    }

    void OnUpdateCatalogs(AsyncOperationHandle<List<IResourceLocator>> handle) {
        Debug.LogError("OnUpdateFinish");
        downloadKeys.Clear();
        foreach (var loc in handle.Result) {
            downloadKeys.AddRange(loc.Keys);
        }
        Addressables.GetDownloadSizeAsync(downloadKeys).Completed += OnCheckDownload;
        Addressables.Release(handle);
    }

    void OnCheckDownload(AsyncOperationHandle<long> checkHandle) {
        Debug.LogError("GetDownloadSizeAsync " + checkHandle.Result.ToString());
        if (checkHandle.Result > 0) {
            Debug.LogError("有需要更新的Handle");
            totalDownloadSize = checkHandle.Result;
            OnCheckCompleteUpdate?.Invoke(checkHandle.Result);
        } else {
            Debug.LogError("NoNeedUpdate");
            OnCheckCompleteNoUpdate?.Invoke();
        }
    }

    public void DownloadAsset() {
        CoroutineController.manager.StartCoroutine(DownloadAssets());
    }

    IEnumerator DownloadAssets() {
        string str = "";
        for (int i = 0; i < downloadKeys.Count; i++) {
            str += downloadKeys[i] + "; ";
        }
        Debug.LogError("DownloadAssets " + str);
        downloadHandle = Addressables.DownloadDependenciesAsync(downloadKeys, Addressables.MergeMode.Union);
        downloadHandle.Completed += OnDonloadComplete;
        if (totalDownloadSize > 0) {
            while (!downloadHandle.IsDone) {
                float percent = downloadHandle.PercentComplete;
                OnUpdate?.Invoke(percent, totalDownloadSize);
                yield return null;
            }
            Addressables.Release(downloadHandle);
        }
    }

    private void OnDonloadComplete(AsyncOperationHandle handle) {
        if (handle.Status == AsyncOperationStatus.Succeeded) {
            OnCompleteDownload?.Invoke();
        } else {
            downloadFailedTimes += 1;
            if (downloadFailedTimes >= 3) {
                //ChangeDownloadUrl(AddressableInfo.cdn_1);
            }
        }
    }

    public void GetAvatarResource(int aid, Action<AsyncOperationHandle<Sprite>> OnLoaded) {
        Addressables.LoadAssetAsync<Sprite>("Assets/AB/Avatar/" + aid + ".png").Completed += OnLoaded;
    }

    public void GetEquipResource(string rid, Action<AsyncOperationHandle<GameObject>> OnLoaded) {
        rid = rid.Trim();
        Addressables.LoadAssetAsync<GameObject>("Assets/AB/Equip/Items/" + rid + ".prefab").Completed += OnLoaded;
    }

    protected override void OnInit() {
        
    }
}
