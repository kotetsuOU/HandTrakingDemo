/*
 * Copyright 2019,2020,2023,2024 Sony Corporation
 */

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using SRD.Utils;

namespace SRD.Core
{
    [System.Serializable]

    internal struct SRDSystemDescription
    {
        public SRDSystemDescription(FaceTrackerSystem faceTracker, EyeViewRendererSystem eyeViewRenderer, StereoCompositerSystem stereoCompositer)
        {
            faceTrackerSystem = faceTracker;
            eyeViewRendererSystem = eyeViewRenderer;
            stereoCompositerSystem = stereoCompositer;
        }
        public FaceTrackerSystem faceTrackerSystem;
        public EyeViewRendererSystem eyeViewRendererSystem;
        public StereoCompositerSystem stereoCompositerSystem;
    }

    internal class SRDCoreRenderer : IDisposable
    {
        public Action<bool> OnSRDFaceTrackStateEvent;

        private SRDSystemDescription _currSystemDesc;
        private ISRDFaceTracker _faceTracker;
        private ISRDEyeViewRenderer _eyeViewRenderer;
        private ISRDStereoCompositer _stereoCompositer;

        private Dictionary<FaceTrackerSystem, ISRDFaceTracker> _cachedFaceTracker;
        private Dictionary<EyeViewRendererSystem, ISRDEyeViewRenderer> _cachedEyeViewRenderer;
        private Dictionary<StereoCompositerSystem, ISRDStereoCompositer> _cachedStereoCompositer;

        private bool _isStereoTextureRegistered;
        private RenderTexture _outputTexture = null;
        private SRDManager _srdManager;

        public SRDCoreRenderer(SRDSystemDescription description, SRDManager srdManager)
        {
            _cachedFaceTracker = new Dictionary<FaceTrackerSystem, ISRDFaceTracker>();
            _cachedEyeViewRenderer = new Dictionary<EyeViewRendererSystem, ISRDEyeViewRenderer>();
            _cachedStereoCompositer = new Dictionary<StereoCompositerSystem, ISRDStereoCompositer>();

            _currSystemDesc = description;

            _srdManager = srdManager;
        }

        public void Update(Transform srdOriginTransform, bool isBoxFrontNearClipActive)
        {
            _faceTracker.UpdateState(srdOriginTransform);
            var xrResult = _eyeViewRenderer.UpdateFacePose(_faceTracker, isBoxFrontNearClipActive);

            if(OnSRDFaceTrackStateEvent != null)
            {
                OnSRDFaceTrackStateEvent.Invoke(xrResult == SrdXrResult.SUCCESS);
            }
        }

        public void RegisterOutputTexture(RenderTexture outputTexture)
        {
            _outputTexture = outputTexture;
        }

        private Texture2D _calibrationLeftTex;
        private Texture2D _calibrationRightTex;

        private bool _isCalibrationRegistered = false;
        private bool _lastIsHalfMirror = false;

        internal bool IsHalfMirrorActive()
        {
            return _srdManager != null && _srdManager.isHalfMirrorEnabled;
        }

        public void Composite()
        {
            bool isHalfMirror = IsHalfMirrorActive();
            if (isHalfMirror != _lastIsHalfMirror)
            {
                _isStereoTextureRegistered = false;
                _lastIsHalfMirror = isHalfMirror;
            }

            if (_calibrationLeftTex == null)
            {
                _calibrationLeftTex = new Texture2D(1, 1);
                _calibrationLeftTex.SetPixel(0, 0, Color.green);
                _calibrationLeftTex.Apply();
            }
            if (_calibrationRightTex == null)
            {
                _calibrationRightTex = new Texture2D(1, 1);
                _calibrationRightTex.SetPixel(0, 0, Color.red);
                _calibrationRightTex.Apply();
            }

            if (_srdManager.EnableCalibrationMode)
            {
                if (!_isStereoTextureRegistered || !_isCalibrationRegistered)
                {
                    var leftCalib = isHalfMirror ? _calibrationRightTex : _calibrationLeftTex;
                    var rightCalib = isHalfMirror ? _calibrationLeftTex : _calibrationRightTex;
                    _stereoCompositer.RegisterSourceStereoTextures(leftCalib, rightCalib);
                    _isStereoTextureRegistered = true;
                    _isCalibrationRegistered = true;
                }
                _stereoCompositer.RenderStereoComposition(_outputTexture);
                return; // キャリブレーション時は以降の通常の描画をスキップ
            }
            
            if (!_isStereoTextureRegistered || _isCalibrationRegistered)
            {
                var leftTex = isHalfMirror ? _eyeViewRenderer.GetRightEyeViewTexture() : _eyeViewRenderer.GetLeftEyeViewTexture();
                var rightTex = isHalfMirror ? _eyeViewRenderer.GetLeftEyeViewTexture() : _eyeViewRenderer.GetRightEyeViewTexture();
                _stereoCompositer.RegisterSourceStereoTextures(leftTex, rightTex);
                _isStereoTextureRegistered = true;
                _isCalibrationRegistered = false;
            }

            _stereoCompositer.RenderStereoComposition(_outputTexture);
        }

        public void Start()
        {
            InitializeFaceTracker(_currSystemDesc.faceTrackerSystem);
            InitializeEyeViewRenderer(_currSystemDesc.eyeViewRendererSystem);
            InitializeStereoCompositer(_currSystemDesc.stereoCompositerSystem);
        }

        public void Stop()
        {
            _faceTracker.Stop();
            _eyeViewRenderer.Stop();
            _stereoCompositer.Stop();
        }

        public void Dispose()
        {
            OnSRDFaceTrackStateEvent = null;

            foreach(var cache in _cachedFaceTracker)
            {
                cache.Value.Dispose();
            }
            foreach(var cache in _cachedEyeViewRenderer)
            {
                cache.Value.Dispose();
            }
            foreach(var cache in _cachedStereoCompositer)
            {
                cache.Value.Dispose();
            }
        }

        public void UpdateSRDSystemsAndRefresh(SRDSystemDescription srdSystemDesc)
        {
            UpdateFaceTrackerAndRefresh(srdSystemDesc.faceTrackerSystem);
            UpdateEyeViewRendererAndRefresh(srdSystemDesc.eyeViewRendererSystem);
            UpdateStereoCompositerAndRefresh(srdSystemDesc.stereoCompositerSystem);
        }

        private void InitializeFaceTracker(FaceTrackerSystem faceTrackerSystem)
        {
            if(!_cachedFaceTracker.TryGetValue(faceTrackerSystem, out _faceTracker))
            {
                _faceTracker = SRDFaceTrackerFactory.CreateFaceTracker(faceTrackerSystem, _srdManager);
                _cachedFaceTracker[faceTrackerSystem] = _faceTracker;
            }

            _currSystemDesc.faceTrackerSystem = faceTrackerSystem;
            _faceTracker.Start();
        }

        private void InitializeEyeViewRenderer(EyeViewRendererSystem eyeViewRendererSystem)
        {
            if(!_cachedEyeViewRenderer.TryGetValue(eyeViewRendererSystem, out _eyeViewRenderer))
            {
                _eyeViewRenderer = SRDEyeViewRendererFactory.CreateEyeViewRenderer(eyeViewRendererSystem, _srdManager);
                _cachedEyeViewRenderer[eyeViewRendererSystem] = _eyeViewRenderer;
            }

            _currSystemDesc.eyeViewRendererSystem = eyeViewRendererSystem;
            _isStereoTextureRegistered = false;

            _eyeViewRenderer.Start();
        }

        private void InitializeStereoCompositer(StereoCompositerSystem stereoCompositerSystem)
        {
            if(!_cachedStereoCompositer.TryGetValue(stereoCompositerSystem, out _stereoCompositer))
            {
                _stereoCompositer = SRDStereoCompositerFactory.CreateStereoCompositer(stereoCompositerSystem, _srdManager);
                _cachedStereoCompositer[stereoCompositerSystem] = _stereoCompositer;
            }

            _currSystemDesc.stereoCompositerSystem = stereoCompositerSystem;
            _stereoCompositer.Start();
        }



        private void UpdateFaceTrackerAndRefresh(FaceTrackerSystem faceTrackerSystem)
        {
            if(_currSystemDesc.faceTrackerSystem == faceTrackerSystem)
            {
                return;
            }
            _faceTracker.Stop();
            InitializeFaceTracker(faceTrackerSystem);
        }

        private void UpdateEyeViewRendererAndRefresh(EyeViewRendererSystem eyeViewRendererSystem)
        {
            if(_currSystemDesc.eyeViewRendererSystem == eyeViewRendererSystem)
            {
                return;
            }
            _eyeViewRenderer.Stop();
            InitializeEyeViewRenderer(eyeViewRendererSystem);
        }

        private void UpdateStereoCompositerAndRefresh(StereoCompositerSystem stereoCompositerSystem)
        {
            if(_currSystemDesc.stereoCompositerSystem == stereoCompositerSystem)
            {
                return;
            }
            _stereoCompositer.Stop();
            InitializeStereoCompositer(stereoCompositerSystem);
        }

    }
}
