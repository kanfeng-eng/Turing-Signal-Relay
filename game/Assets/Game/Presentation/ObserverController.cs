using System;
using System.Collections;
using System.IO;
using System.IO.Compression;
using UnityEngine;
using UnityEngine.Networking;

namespace TuringSignalRelay
{
    public sealed class ObserverController : MonoBehaviour
    {
        private readonly ObserverPlayback playback = new ObserverPlayback();
        public ObserverPlayback Playback
        {
            get
            {
                return playback;
            }
        }
        private readonly LiveFrameBuffer liveBuffer = new LiveFrameBuffer();
        public LiveFrameBuffer LiveBuffer
        {
            get
            {
                return liveBuffer;
            }
        }
        private string status = "Local fixture";
        public string Status
        {
            get
            {
                return status;
            }
            private set
            {
                status = value;
            }
        }
        private string error = "";
        public string Error
        {
            get
            {
                return error;
            }
            private set
            {
                error = value;
            }
        }
        private int side = -1;
        public int Side
        {
            get
            {
                return side;
            }
            private set
            {
                side = value;
            }
        }
        public bool IsLive
        {
            get; private set;
        }
        public bool Hold
        {
            get; private set;
        }
        private string endpoint = "", token = "";
        private int generation;
        public int SourceVersion { get; private set; }
        private UnityWebRequest pendingRequest;
        private readonly ObserverFrame waiting = new ObserverFrame
        {
            protocolVersion = 1,
            type = "observer-frame",
            terrain = new int[9216],
            visibleToMask = new int[9216],
            rulesVersion = "waiting-for-live-frame"
        };
        public ObserverFrame Frame
        {
            get
            {
                return IsLive ? LiveBuffer.Current ?? waiting : Playback.Current;
            }
        }

        private void Start()
        {
            if (Application.isBatchMode)
            {
                enabled = false;
                return;
            }
            string[] arguments = Environment.GetCommandLineArgs();
            if (Array.IndexOf(arguments, "-screen-width") < 0 && Array.IndexOf(arguments, "-screen-height") < 0 && Array.IndexOf(arguments, "-screen-fullscreen") < 0)
                Screen.SetResolution(Math.Min(2560, Display.main.systemWidth - 100), Math.Min(1600, Display.main.systemHeight - 160), false);
            LoadDemo();
            gameObject.AddComponent<ObserverHud>().Initialize(this);
            StartCoroutine(Poll());
        }
        public void LoadDemo()
        {
            var demo = Resources.Load<TextAsset>("ObserverDemo");
            using (var input = new MemoryStream(demo.bytes))
            using (var gzip = new GZipStream(input, CompressionMode.Decompress))
            using (var reader = new StreamReader(gzip))
                Playback.Load(reader.ReadToEnd());
            SourceVersion++;
        }
        private void Update()
        {
            if (!IsLive)
                Playback.Advance(Time.unscaledDeltaTime);
        }
        public void SelectSide(int side)
        {
            if (side < -1 || side > 1)
                throw new ArgumentOutOfRangeException("side");
            Side = side;
        }
        public void TogglePlayback()
        {
            if (IsLive)
            {
                Hold = !Hold;
                generation++;
                if (pendingRequest != null)
                    pendingRequest.Abort();
            }
            else
                Playback.Playing = !Playback.Playing;
        }
        public void LoadReplay(string path)
        {
            try
            {
                if (path.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase))
                    Playback.LoadBootstrap(path);
                else
                    Playback.Load(File.ReadAllText(path));
                IsLive = false;
                generation++;
                if (pendingRequest != null)
                    pendingRequest.Abort();
                Status = "Replay loaded";
                Error = "";
                SourceVersion++;
            }
            catch (Exception exception) { Playback.Playing = false; Error = "Load failed: " + exception.Message; }
        }
        public void ConnectionSettings(string address, string accessToken)
        {
            if (endpoint == address && token == accessToken)
                return;
            endpoint = address;
            token = accessToken;
            if (pendingRequest != null)
                pendingRequest.Abort();
            if (IsLive)
            {
                IsLive = false;
                generation++;
                LiveBuffer.Reset();
                Status = "Connection changed. Press Connect.";
                SourceVersion++;
            }
        }
        public void ToggleConnection()
        {
            generation++;
            if (pendingRequest != null)
                pendingRequest.Abort();
            if (IsLive)
            {
                IsLive = false;
                Status = "Disconnected";
                SourceVersion++;
                return;
            }
            Uri uri;
            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out uri) || !(uri.Scheme == "https" || uri.Scheme == "http" && uri.IsLoopback))
            {
                Error = "Use HTTPS or a local test URL.";
                return;
            }
            IsLive = true;
            SourceVersion++;
            Hold = false;
            LiveBuffer.Reset();
            Status = "Connecting";
            Error = "";
        }
        private IEnumerator Poll()
        {
            while (true)
            {
                if (IsLive && !Hold)
                {
                    int requestedGeneration = generation;
                    using (var request = UnityWebRequest.Get(endpoint))
                    {
                        pendingRequest = request;
                        try
                        {
                            request.timeout = 5;
                            request.redirectLimit = 0;
                            if (!string.IsNullOrWhiteSpace(token))
                                request.SetRequestHeader("Authorization", "Bearer " + token);
                            yield return request.SendWebRequest();
                            if (IsLive && !Hold && requestedGeneration == generation)
                            {
                                if (request.result != UnityWebRequest.Result.Success)
                                    Status = "Disconnected: " + request.error;
                                else
                                {
                                    try
                                    {
                                        Status = LiveBuffer.AcceptJson(request.downloadHandler.text) ? "Live / " + LiveBuffer.Status + " / tick " + LiveBuffer.Current.tick : "Waiting for newer frame";
                                    }
                                    catch (Exception exception) { Status = "Invalid live frame: " + exception.Message; }
                                }
                            }
                        }
                        finally { pendingRequest = null; }
                    }
                }
                yield return new WaitForSecondsRealtime(.5f);
            }
        }
        private void OnDestroy()
        {
            if (pendingRequest != null)
                pendingRequest.Abort();
        }
    }
}
