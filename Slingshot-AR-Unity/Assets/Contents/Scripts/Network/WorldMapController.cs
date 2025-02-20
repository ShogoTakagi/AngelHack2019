﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.UI;
using System.IO;
using Unity.Collections;
#if UNITY_IOS
using UnityEngine.XR.ARKit;
#endif

public class WorldMapController : MonoBehaviour
{
    public enum WorldMapState
    {
        None,
        Sending,
        Loading,
        Successed,
        Failed,
        Synchronized
    }

    public WorldMapState mapState { get; set; }
    string worldmapName = "SlingShotMap";

    public ARSession _session { get; set; }

    ARWorldMappingStatus _status = ARWorldMappingStatus.NotAvailable;

    System.Action waitCreate;

    System.Action waitFound;

    private void Start()
    {
        _session = null;
    }

    private void Update()
    {
        if(_session != null)
        {
#if UNITY_IOS
            var sessionSubsystem = (ARKitSessionSubsystem)_session.subsystem;
#else
        XRSessionSubsystem sessionSubsystem = null;
#endif
            if (sessionSubsystem == null)
                return;
            if(_status != sessionSubsystem.worldMappingStatus)
            {
                Debug.Log(string.Format("Mapping Status Changed: {0}", sessionSubsystem.worldMappingStatus));
                _status = sessionSubsystem.worldMappingStatus;
                if(_status == ARWorldMappingStatus.Mapped)
                {
                    if(waitCreate != null)
                    {
                        waitCreate.Invoke();
                        waitCreate = null;
                    }
                    else if(waitFound != null)
                    {
                        waitFound.Invoke();
                        waitFound = null;
                    }
                }
            }
        }

    }

    #region public methods
    public void SaveStart(System.Action<bool> savedAction)
    {
        Debug.Log("Save Start");
        StartCoroutine(Save(savedAction));
    }

    public void LoadStart(System.Action<bool> loadedAction)
    {
        IEnumerator _load = Load(loadedAction);
        Debug.Log("Load Start");
        StartCoroutine(Load(loadedAction));
    }

    public void CreateWorldMap(System.Action onCreated)
    {
        waitCreate = onCreated;
    }

    public void FoundWorldMap(System.Action onFounded)
    {
        waitFound = onFounded;
    }
    #endregion


#if UNITY_IOS
    IEnumerator Save(System.Action<bool> onFinished)
    {
        var sessionSubsystem = (ARKitSessionSubsystem)_session.subsystem;
        if (sessionSubsystem == null)
        {
            Debug.LogError("No session subsystem available. Could not save.");
            onFinished?.Invoke(false);
            yield break;
        }

        var request = sessionSubsystem.GetARWorldMapAsync();
        Debug.Log("Wait Request Done...");
        while (!request.status.IsDone())
            yield return null;

        if (request.status.IsError())
        {
            Debug.Log(string.Format("Session serialization failed with status {0}", request.status));
            onFinished?.Invoke(false);
            yield break;
        }

        var worldMap = request.GetWorldMap();
        request.Dispose();

        Debug.Log("Request Done");
        SaveAndDisposeWorldMap(worldMap);

        onFinished?.Invoke(true);
    }

    IEnumerator Load(System.Action<bool> onFinished)
    {
        var sessionSubsystem = (ARKitSessionSubsystem)_session.subsystem;
        if (sessionSubsystem == null)
        {
            Debug.Log("No session subsystem available. Could not load.");
            onFinished?.Invoke(false);
            yield break;
        }

        var file = File.Open(path, FileMode.Open);
        if (file == null)
        {
            Debug.Log(string.Format("File {0} does not exist.", path));
            onFinished?.Invoke(false);
            yield break;
        }

        Debug.Log(string.Format("Reading {0}...", path));

        int bytesPerFrame = 1024 * 10;
        var bytesRemaining = file.Length;
        var binaryReader = new BinaryReader(file);
        var allBytes = new List<byte>();
        while (bytesRemaining > 0)
        {
            var bytes = binaryReader.ReadBytes(bytesPerFrame);
            allBytes.AddRange(bytes);
            bytesRemaining -= bytesPerFrame;
            yield return null;
        }

        var data = new NativeArray<byte>(allBytes.Count, Allocator.Temp);
        data.CopyFrom(allBytes.ToArray());

        Debug.Log(string.Format("Deserializing to ARWorldMap...", path));
        ARWorldMap worldMap;
        if (ARWorldMap.TryDeserialize(data, out worldMap))
            data.Dispose();

        if (worldMap.valid)
        {
            Debug.Log("Deserialized successfully.");
        }
        else
        {
            Debug.LogError("Data is not a valid ARWorldMap.");
            onFinished?.Invoke(false);
            yield break;
        }

        Debug.Log("Apply ARWorldMap to current session.");
        sessionSubsystem.ApplyWorldMap(worldMap);

        onFinished?.Invoke(true);
    }

    void SaveAndDisposeWorldMap(ARWorldMap worldMap)
    {
        Debug.Log("Serializing ARWorldMap to byte array...");
        var data = worldMap.Serialize(Allocator.Temp);
        Debug.Log(string.Format("ARWorldMap has {0} bytes.", data.Length));

        var file = File.Open(path, FileMode.Create);
        var writer = new BinaryWriter(file);
        writer.Write(data.ToArray());
        writer.Close();
        data.Dispose();
        worldMap.Dispose();
        Debug.Log(string.Format("ARWorldMap written to {0}", path));
    }
#endif

    string path
    {
        get
        {
            string filename = worldmapName + ".worldmap";
            return Path.Combine(Application.persistentDataPath, filename);
        }
    }
}
