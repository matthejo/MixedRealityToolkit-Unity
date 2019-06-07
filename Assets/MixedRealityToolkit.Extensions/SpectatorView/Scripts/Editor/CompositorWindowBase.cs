﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Extensions.Experimental.SpectatorView.Compositor;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using UnityEditor;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions.Experimental.SpectatorView.Editor
{
    internal class CompositorWindowBase<TWindow> : EditorWindowBase<TWindow> where TWindow : EditorWindowBase<TWindow>
    {
        protected const string HolographicCameraDeviceTypeLabel = "Holographic Camera";
        protected const string AppDeviceTypeLabel = "App";
        protected string holographicCameraIPAddress;

        private CompositionManager cachedCompositionManager;
        private HolographicCameraObserver cachedHolographicCameraObserver;

        protected int renderFrameWidth;
        protected int renderFrameHeight;
        protected float aspect;
        private float uiFrameWidth = 100;
        private float uiFrameHeight = 100;

        private const string notConnectedMessage = "Not connected";
        private const string trackingLostStatusMessage = "Tracking lost";
        private const string trackingStalledStatusMessage = "No tracking update in over a second";
        private const string locatingSharedSpatialCoordinate = "Locating shared spatial coordinate...";
        private const string notLocatedSharedSpatialCoordinate = "Not located";
        private const string locatedSharedSpatialCoordinateMessage = "Located";
        private const string calibrationLoadedMessage = "Loaded";
        private const string calibrationNotLoadedMessage = "No camera calibration received";
        private const int horizontalFrameRectangleMargin = 50;
        protected const int textureRenderModeComposite = 0;
        protected const int textureRenderModeSplit = 1;
        private const float quadPadding = 4;
        private const int connectAndDisconnectButtonWidth = 90;

        protected virtual void OnEnable()
        {
            renderFrameWidth = CompositionManager.GetVideoFrameWidth();
            renderFrameHeight = CompositionManager.GetVideoFrameHeight();
            aspect = ((float)renderFrameWidth) / renderFrameHeight;
        }

        protected void HolographicCameraNetworkConnectionGUI(string deviceTypeLabel, DeviceInfoObserver deviceInfo, SpatialCoordinateSystemParticipant spatialCoordinateSystemParticipant, bool showCalibrationStatus, ref string ipAddressField)
        {
            GUIStyle boldLabelStyle = new GUIStyle(GUI.skin.label);
            boldLabelStyle.fontStyle = FontStyle.Bold;

            CompositionManager compositionManager = GetCompositionManager();
            
            EditorGUILayout.BeginVertical("Box");
            {
                Color titleColor;
                if (deviceInfo != null &&
                    deviceInfo.NetworkManager != null &&
                    deviceInfo.NetworkManager.IsConnected &&
                    spatialCoordinateSystemParticipant != null &&
                    spatialCoordinateSystemParticipant.PeerDeviceHasTracking &&
                    spatialCoordinateSystemParticipant.PeerSpatialCoordinateIsLocated &&
                    !deviceInfo.IsTrackingStalled &&
                    (!showCalibrationStatus || (compositionManager != null &&
                    compositionManager.IsCalibrationDataLoaded)))
                {
                    titleColor = Color.green;
                }
                else
                {
                    titleColor = Color.yellow;
                }
                RenderTitle(deviceTypeLabel, titleColor);

                if (deviceInfo != null && deviceInfo.NetworkManager != null && deviceInfo.NetworkManager.IsConnected)
                {
                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.Label("Connection status", boldLabelStyle);

                        if (GUILayout.Button(new GUIContent("Disconnect", "Disconnects the network connection to the holographic camera."), GUILayout.Width(connectAndDisconnectButtonWidth)))
                        {
                            deviceInfo.NetworkManager.Disconnect();
                        }
                    }
                    GUILayout.EndHorizontal();

                    if (deviceInfo.NetworkManager.ConnectedIPAddress == deviceInfo.DeviceIPAddress)
                    {
                        GUILayout.Label($"Connected to {deviceInfo.DeviceName} ({deviceInfo.DeviceIPAddress})");
                    }
                    else
                    {
                        GUILayout.Label($"Connected to {deviceInfo.DeviceName} ({deviceInfo.NetworkManager.ConnectedIPAddress} -> {deviceInfo.DeviceIPAddress})");
                    }

                    EditorGUILayout.Space();
                }
                else
                {
                    GUILayout.BeginHorizontal();
                    {
                        ipAddressField = EditorGUILayout.TextField(ipAddressField);
                        ConnectButtonGUI(ipAddressField, deviceInfo);
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.Label(notConnectedMessage);
                    EditorGUILayout.Space();
                }

                GUI.enabled = deviceInfo != null && deviceInfo.NetworkManager != null && deviceInfo.NetworkManager.IsConnected && spatialCoordinateSystemParticipant != null;
                string sharedSpatialCoordinateStatusMessage;
                if (deviceInfo == null || deviceInfo.NetworkManager == null || !deviceInfo.NetworkManager.IsConnected || spatialCoordinateSystemParticipant == null)
                {
                    sharedSpatialCoordinateStatusMessage = notConnectedMessage;
                }
                else if (!spatialCoordinateSystemParticipant.PeerDeviceHasTracking)
                {
                    sharedSpatialCoordinateStatusMessage = trackingLostStatusMessage;
                }
                else if (deviceInfo.IsTrackingStalled)
                {
                    sharedSpatialCoordinateStatusMessage = trackingStalledStatusMessage;
                }
                else if (spatialCoordinateSystemParticipant.PeerIsLocatingSpatialCoordinate)
                {
                    sharedSpatialCoordinateStatusMessage = locatingSharedSpatialCoordinate;
                }
                else if (!spatialCoordinateSystemParticipant.PeerSpatialCoordinateIsLocated)
                {
                    sharedSpatialCoordinateStatusMessage = notLocatedSharedSpatialCoordinate;
                }
                else
                {
                    sharedSpatialCoordinateStatusMessage = locatedSharedSpatialCoordinateMessage;
                }

                GUILayout.Label("Shared spatial coordinate status", boldLabelStyle);
                GUILayout.Label(sharedSpatialCoordinateStatusMessage);

                string calibrationStatusMessage;
                if (compositionManager != null && compositionManager.IsCalibrationDataLoaded)
                {
                    calibrationStatusMessage = calibrationLoadedMessage;
                }
                else
                {
                    calibrationStatusMessage = calibrationNotLoadedMessage;
                }

                EditorGUILayout.Space();
                if (showCalibrationStatus)
                {
                    GUILayout.Label("Calibration status", boldLabelStyle);
                    GUILayout.Label(calibrationStatusMessage);
                }
                else
                {
                    // Empty label to remain as a placeholder for the Calibration status
                    GUILayout.Label(string.Empty, boldLabelStyle);
                    GUILayout.Label(string.Empty);
                }

                EditorGUILayout.Space();

                if (GUILayout.Button(new GUIContent("Attach To Persisted Coordinate", "Detects the shared location used to position objects in the same physical location on multiple devices")))
                {
                    SpatialCoordinateSystemManager.Instance.InitializeRemoteSpatialCoordinate(deviceInfo.ConnectedEndpoint, MarkerDetectorSpatialLocalizer.ID, new MarkerDetectorLocalizationSettings
                    {
                        MarkerID = 0,
                        MarkerSize = 0.1f,
                        ShouldPersistCoordinate = true
                    });
                }

                if (GUILayout.Button(new GUIContent("Locate Shared Spatial Coordinate", "Detects the shared location used to position objects in the same physical location on multiple devices")))
                {
                    SpatialCoordinateSystemManager.Instance.InitiateRemoteLocalization(deviceInfo.ConnectedEndpoint, MarkerDetectorSpatialLocalizer.ID, new MarkerDetectorLocalizationSettings
                    {
                        MarkerID = 0,
                        MarkerSize = 0.1f,
                        ShouldPersistCoordinate = true
                    });
                }

                GUI.enabled = true;
            }
            EditorGUILayout.EndVertical();
        }

        protected virtual Rect ComputeCompositeGUIRect(float frameWidth, float frameHeight)
        {
            return GUILayoutUtility.GetRect(frameWidth, frameHeight);
        }

        protected void CompositeTextureGUI(int textureRenderMode)
        {
            UpdateFrameDimensions();

            Rect framesRect = ComputeCompositeGUIRect(uiFrameWidth, uiFrameHeight);

            if (Event.current != null && Event.current.type == EventType.Repaint)
            {
                CompositionManager compositionManager = GetCompositionManager();
                if (compositionManager != null && compositionManager.TextureManager != null)
                {
                    if (textureRenderMode == textureRenderModeSplit)
                    {
                        Rect[] quadrantRects = CalculateVideoQuadrants(framesRect);
                        if (compositionManager.TextureManager.compositeTexture != null)
                        {
                            Graphics.DrawTexture(quadrantRects[0], compositionManager.TextureManager.compositeTexture);
                        }

                        if (compositionManager.TextureManager.colorRGBTexture != null)
                        {
                            Graphics.DrawTexture(quadrantRects[1], compositionManager.TextureManager.colorRGBTexture, compositionManager.TextureManager.IgnoreAlphaMaterial);
                        }

                        if (compositionManager.TextureManager.renderTexture != null)
                        {
                            Graphics.DrawTexture(quadrantRects[2], compositionManager.TextureManager.renderTexture, compositionManager.TextureManager.IgnoreAlphaMaterial);
                        }

                        if (compositionManager.TextureManager.alphaTexture != null)
                        {
                            Graphics.DrawTexture(quadrantRects[3], compositionManager.TextureManager.alphaTexture, compositionManager.TextureManager.IgnoreAlphaMaterial);
                        }
                    }
                    else
                    {
                        Graphics.DrawTexture(framesRect, compositionManager.TextureManager.compositeTexture);
                    }
                }
            }
        }

        protected void ConnectButtonGUI(string targetIpString, DeviceInfoObserver remoteDevice)
        {
            string tooltip = string.Empty;
            IPAddress targetIp;
            bool validAddress = ParseAddress(targetIpString, out targetIp);

            if (remoteDevice == null)
            {
                tooltip = $"{nameof(HolographicCameraObserver)} is missing from the scene.";
            }
            else if (!Application.isPlaying)
            {
                tooltip = "The scene must be in play mode to connect.";
            }
            else if (!validAddress)
            {
                tooltip = "The IP address for the remote device is not valid.";
            }

            GUI.enabled = validAddress && Application.isPlaying && remoteDevice != null;
            string label = remoteDevice != null && remoteDevice.NetworkManager != null && remoteDevice.NetworkManager.IsConnecting ? "Disconnect" : "Connect";
            if (GUILayout.Button(new GUIContent(label, tooltip), GUILayout.Width(connectAndDisconnectButtonWidth)) && remoteDevice != null)
            {
                if (remoteDevice.NetworkManager.IsConnecting)
                {
                    remoteDevice.NetworkManager.Disconnect();
                }
                else
                {
                    remoteDevice.NetworkManager.ConnectTo(targetIpString);
                }
            }
            GUI.enabled = true;
        }

        private bool ParseAddress(string targetIpString, out IPAddress targetIp)
        {
            if (targetIpString == "localhost")
            {
                targetIp = IPAddress.Loopback;
                return true;
            }
            else
            {
                return IPAddress.TryParse(targetIpString, out targetIp);
            }
        }

        private void UpdateFrameDimensions()
        {
            uiFrameWidth = position.width - horizontalFrameRectangleMargin;
            uiFrameHeight = position.height;

            if (uiFrameWidth <= uiFrameHeight * aspect)
            {
                uiFrameHeight = uiFrameWidth / aspect;
            }
            else
            {
                uiFrameWidth = uiFrameHeight * aspect;
            }
        }

        private Rect[] CalculateVideoQuadrants(Rect videoRect)
        {
            float quadWidth = videoRect.width / 2 - quadPadding / 2;
            float quadHeight = videoRect.height / 2 - quadPadding / 2;
            Rect[] rects = new Rect[4];
            rects[0] = new Rect(videoRect.x, videoRect.y, quadWidth, quadHeight);
            rects[1] = new Rect(videoRect.x + quadWidth + quadPadding, videoRect.y, quadWidth, quadHeight);
            rects[2] = new Rect(videoRect.x, videoRect.y + quadHeight + quadPadding, quadWidth, quadHeight);
            rects[3] = new Rect(videoRect.x + quadWidth + quadPadding, videoRect.y + quadHeight + quadPadding, quadWidth, quadHeight);
            return rects;
        }

        protected CompositionManager GetCompositionManager()
        {
            if (cachedCompositionManager == null)
            {
                cachedCompositionManager = FindObjectOfType<CompositionManager>();
            }

            return cachedCompositionManager;
        }

        protected HolographicCameraObserver GetHolographicCameraObserver()
        {
            if (cachedHolographicCameraObserver == null)
            {
                cachedHolographicCameraObserver = FindObjectOfType<HolographicCameraObserver>();
            }

            return cachedHolographicCameraObserver;
        }

        protected DeviceInfoObserver GetHolographicCameraDevice()
        {
            HolographicCameraObserver observer = GetHolographicCameraObserver();
            if (observer != null)
            {
                return observer.GetComponent<DeviceInfoObserver>();
            }
            else
            {
                return null;
            }
        }

        protected SpatialCoordinateSystemParticipant GetSpatialCoordinateSystemParticipant(DeviceInfoObserver device)
        {
            if (device != null && device.ConnectedEndpoint != null && SpatialCoordinateSystemManager.IsInitialized)
            {
                if (SpatialCoordinateSystemManager.Instance.TryGetSpatialCoordinateSystemParticipant(device.ConnectedEndpoint, out SpatialCoordinateSystemParticipant participant))
                {
                    return participant;
                }
                else
                {
                    Debug.LogError("Expected to be able to find participant for an endpoint");
                    return null;
                }
            }
            else
            {
                return null;
            }
        }
    }
}