using System;

using UnityEngine;
using UnityEngine.Assertions;

#if UNITY_EDITOR
using UnityEditor;
#endif

using PreUpdate = UnityEngine.PlayerLoop.PreUpdate;
using PostLateUpdate = UnityEngine.PlayerLoop.PostLateUpdate;
using EarlyUpdate = UnityEngine.PlayerLoop.EarlyUpdate;

namespace Unity.SnapshotDebugger
{
    public class Debugger
    {
        public enum State
        {
            Suspended,
            Record,
        }

        public float deltaTime
        {
            get; private set;
        }

        public float time
        {
            get; private set;
        }

        public float rewindTime
        {
            get; set;
        }

        public bool rewind
        {
            get
            {
                return _rewind;
            }

            set
            {
                if (_rewind && !value)
                {
                    rewindTime = _storage.endTimeInSeconds;

                    OnPreUpdate();
                }

                _rewind = value;
            }
        }

        public float capacityInSeconds
        {
            get
            {
                return _preferences.capacityInSeconds;
            }

            set
            {
                _preferences.capacityInSeconds = value;

                _preferences.Save();

                _storage.capacityInSeconds = value;
            }
        }

        public int memorySize
        {
            get { return _storage.memorySize; }
        }

        public float startTimeInSeconds
        {
            get { return _storage.startTimeInSeconds; }
        }

        public float endTimeInSeconds
        {
            get { return _storage.endTimeInSeconds; }
        }

        public static Debugger instance
        {
            get
            {
                if (_instance == null)
                {
                    Initialize();
                }

                return _instance;
            }
        }

        private static Debugger _instance;

        internal static Registry registry
        {
            get { return instance._registry; }
        }

        internal static FrameDebugger frameDebugger
        {
            get { return instance._frameDebugger; }
        }

        public Identifier<Aggregate> this[GameObject gameObject]
        {
            get
            {
                var aggregate = registry[gameObject];

                if (aggregate != null)
                {
                    return aggregate.identifier;
                }

                return Identifier<Aggregate>.Undefined;
            }
        }

        public GameObject this[Identifier<Aggregate> identifier]
        {
            get { return registry[identifier].gameObject; }
        }

        public bool IsRecording
        {
            get { return IsState(State.Record); }
        }

        public bool IsState(State state)
        {
            return this.state == state;
        }

        public State state
        {
            get
            {
                return _preferences.state;
            }

            set
            {
                _preferences.state = value;

                _preferences.Save();
            }
        }

        [Serializable]
        struct Preferences
        {
            public State state;
            public float capacityInSeconds;


            public static Preferences Load()
            {
#if UNITY_EDITOR
                var json = EditorPrefs.GetString(preferencesKey);

                if (!string.IsNullOrEmpty(json))
                {
                    return JsonUtility.FromJson<Preferences>(json);
                }
#endif

                return new Preferences
                {
                    state = State.Suspended,
                    capacityInSeconds = 10.0f
                };
            }

            public void Save()
            {
#if UNITY_EDITOR
                var json = JsonUtility.ToJson(this);

                EditorPrefs.SetString(preferencesKey, json);
#endif
            }

            const string preferencesKey = "Unity.SnapshotDebugger.Preferences";
        }

        Debugger()
        {
            deltaTime = Time.deltaTime;

            _preferences = Preferences.Load();

            _storage = MemoryStorage.Create(capacityInSeconds);

            _frameDebugger = new FrameDebugger();

#if UNITY_EDITOR
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#endif
        }

        public static void Initialize()
        {
            Assert.IsTrue(_instance == null);

            ReadExtensions.InitializeExtensionMethods();
            WriteExtensions.InitializeExtensionMethods();

            _instance = new Debugger();
        }

#if UNITY_EDITOR
        void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                BeginRecording();
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                StopRecording();
            }
        }

#endif

        void BeginRecording()
        {
            DiscardRecording();

            _storage.PrepareWrite();

            ResetTimer();

            UpdateSystem.Listen<EarlyUpdate>(OnEarlyUpdate);
            UpdateSystem.Listen<PreUpdate>(OnPreUpdate);
            UpdateSystem.Listen<PostLateUpdate>(OnPostLateUpdate);
        }

        void StopRecording()
        {
            UpdateSystem.Ignore<PostLateUpdate>(OnPostLateUpdate);
            UpdateSystem.Ignore<PreUpdate>(OnPreUpdate);
            UpdateSystem.Ignore<EarlyUpdate>(OnEarlyUpdate);

            _storage.Commit();

            _storage.PrepareRead();

            _frameDebugger.Clear();

            ResetTimer();
        }

        void ResetTimer()
        {
            time = 0.0f;
            rewindTime = 0.0f;
            deltaTime = 0.0f;

            _previousDeltaTime = 0.0f;
        }

        void DiscardRecording()
        {
            _storage.Discard();
        }

        void OnEarlyUpdate()
        {
            if (!rewind)
            {
                _previousDeltaTime = deltaTime;

                deltaTime = Time.deltaTime;
            }

            registry.OnEarlyUpdate(rewind);
        }

        void OnPreUpdate()
        {
            if (IsState(State.Record))
            {
                if (rewind)
                {
                    var snapshot = _storage.Retrieve(rewindTime);

                    if (snapshot != null)
                    {
                        time = snapshot.startTimeInSeconds;
                        deltaTime = snapshot.durationInSeconds;

                        registry.RestoreSnapshot(snapshot);
                    }
                }
                else
                {
                    rewindTime = time;

                    time += _previousDeltaTime;

                    _storage.Record(
                        registry.RecordSnapshot(
                            time, deltaTime));

                    _frameDebugger.Update(time, deltaTime, startTimeInSeconds);
                }
            }
        }

        void OnPostLateUpdate()
        {
            if (IsState(State.Record))
            {
                if (!rewind)
                {
                    var snapshot = _storage.Retrieve(time);

                    Assert.IsTrue(snapshot != null);

                    snapshot.PostProcess();
                }
            }
        }

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        static void OnLoadMethod()
        {
            Initialize();
        }

#endif

        Registry _registry = new Registry();

        float _previousDeltaTime;

        Preferences _preferences;

        Storage _storage;

        FrameDebugger _frameDebugger;

        bool _rewind;
    }
}
