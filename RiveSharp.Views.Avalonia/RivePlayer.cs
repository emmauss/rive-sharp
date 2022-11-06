// Copyright 2022 Rive

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Platform;
using Avalonia.Threading;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace RiveSharp.Views
{
    public class RivePlayer : Control
    {
        public static readonly StyledProperty<string> AnimationProperty =
            AvaloniaProperty.Register<RivePlayer, string>(nameof(Animation));
        public static readonly StyledProperty<string> StateMachineProperty =
            AvaloniaProperty.Register<RivePlayer, string>(nameof(StateMachine));
        public static readonly StyledProperty<string> ArtboardProperty =
            AvaloniaProperty.Register<RivePlayer, string>(nameof(Artboard));
        public static readonly StyledProperty<string> SourceProperty =
            AvaloniaProperty.Register<RivePlayer, string>(nameof(Source));
        public static readonly StyledProperty<StateMachineInputCollection> StateMachineInputsProperty =
            AvaloniaProperty.Register<RivePlayer, StateMachineInputCollection>(nameof(StateMachineInputs));

        public string Animation
        {
            get => GetValue(AnimationProperty);
            set => SetValue(AnimationProperty, value);
        }
        public string StateMachine
        {
            get => GetValue(StateMachineProperty);
            set => SetValue(StateMachineProperty, value);
        }
        public string Artboard
        {
            get => GetValue(ArtboardProperty);
            set => SetValue(ArtboardProperty, value);
        }
        public string Source
        {
            get => GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        [Content]
        public StateMachineInputCollection StateMachineInputs
        {
            get => (StateMachineInputCollection)GetValue(StateMachineInputsProperty);
            set => SetValue(StateMachineInputsProperty, value);
        }

        public override void Render(DrawingContext context)
        {
            context.Custom(new CustomDrawing(this, (canvas) =>
            {
                // Handle pending scene actions from the main thread.
                while (_sceneActionsQueue.TryDequeue(out var action))
                {
                    action();
                }

                if (!_scene.IsLoaded)
                {
                    return;
                }

                // Run the animation.
                var now = DateTime.Now;
                if (_lastPaintTime != null)
                {
                    _scene.AdvanceAndApply((now - _lastPaintTime).TotalSeconds);
                }
                _lastPaintTime = now;

                var renderer = new Renderer(canvas);
                renderer.Save();
                renderer.Transform(ComputeAlignment(Bounds.Width, Bounds.Height));
                _scene.Draw(renderer);
                renderer.Restore();
            }));
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == AnimationProperty)
            {
                var newAnimationName = (string)change.NewValue;
                _sceneActionsQueue.Enqueue(() => _animationName = newAnimationName);
                // If a file is currently loading async, it will apply the new animation once it completes.
                if (_activeSourceFileLoader == null)
                {
                    _sceneActionsQueue.Enqueue(() => UpdateScene(SceneUpdates.AnimationOrStateMachine));
                }
            }
            else if (change.Property == ArtboardProperty)
            {
                var newArtboardName = (string)change.NewValue;
                _sceneActionsQueue.Enqueue(() => _artboardName = newArtboardName);
                if (_activeSourceFileLoader != null)
                {
                    // If a file is currently loading async, it will apply the new artboard once
                    // it completes. Loading a new artboard also invalidates any state machine
                    // inputs that were waiting for the file load.
                    _deferredSMInputsDuringFileLoad.Clear();
                }
                else
                {
                    _sceneActionsQueue.Enqueue(() => UpdateScene(SceneUpdates.Artboard));
                }
            }
            else if (change.Property == StateMachineProperty)
            {
                var newStateMachineName = (string)change.NewValue;
                _sceneActionsQueue.Enqueue(() => _stateMachineName = newStateMachineName);
                if (_activeSourceFileLoader != null)
                {
                    // If a file is currently loading async, it will apply the new state machine
                    // once it completes. Loading a new state machine also invalidates any state
                    // machine inputs that were waiting for the file load.
                    _deferredSMInputsDuringFileLoad.Clear();
                }
                else
                {
                    _sceneActionsQueue.Enqueue(() => UpdateScene(SceneUpdates.AnimationOrStateMachine));
                }
            }
            else if (change.Property == SourceProperty)
            {
                var newSourceName = (string)change.NewValue;
                // Clear the current Scene while we wait for the new one to load.
                _sceneActionsQueue.Enqueue(() => _scene = new Scene());
                if (_activeSourceFileLoader != null)
                {
                    _activeSourceFileLoader.Cancel();
                }

                _activeSourceFileLoader = new CancellationTokenSource();
                // Defer state machine inputs here until the new file is loaded.
                _deferredSMInputsDuringFileLoad = new List<Action>();
                LoadSourceFileDataAsync(newSourceName, _activeSourceFileLoader.Token);
            }
        }


        private CancellationTokenSource _activeSourceFileLoader = null;

        public RivePlayer()
        {
            this.StateMachineInputs = new StateMachineInputCollection(this);
            this.Loaded += OnLoaded;
            this.PointerPressed +=
                (object s, PointerPressedEventArgs e) => HandlePointerEvent(_scene.PointerDown, e);
            this.PointerMoved +=
                (object s, PointerEventArgs e) => HandlePointerEvent(_scene.PointerMove, e);
            this.PointerReleased +=
                (object s, PointerReleasedEventArgs e) => HandlePointerEvent(_scene.PointerUp, e);
        }

        private async void LoadSourceFileDataAsync(string name, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(name))
            {
                _sceneActionsQueue.Enqueue(() => UpdateScene(SceneUpdates.File, null));

                return;
            }

            byte[] data = null;

            try
            {
                var assets = AvaloniaLocator.Current.GetService<IAssetLoader>();
                using var stream = assets.Open(new Uri(name));
                using var mem = new MemoryStream();
                stream.CopyTo(mem);

                data = mem.ToArray();
            }
            catch (Exception ex)
            {
                if (name.StartsWith("http") && Uri.TryCreate(name, UriKind.Absolute, out var uri))
                {
                    var client = new WebClient();
                    data = await client.DownloadDataTaskAsync(uri);
                }
                else
                {
                    var storageProvider = (VisualRoot as TopLevel)?.StorageProvider;
                    if (storageProvider != null)
                    {
                        var file = await storageProvider.OpenFileBookmarkAsync(name);

                        if (file.CanOpenRead)
                        {
                            using var stream = await file.OpenReadAsync();
                            using var mem = new MemoryStream();
                            stream.CopyTo(mem);

                            data = mem.ToArray();
                        }
                    }
                }
            }

            if (data != null && !cancellationToken.IsCancellationRequested)
            {
                _sceneActionsQueue.Enqueue(() => UpdateScene(SceneUpdates.File, data));
                // Apply deferred state machine inputs once the scene is fully loaded.
                foreach (Action stateMachineInput in _deferredSMInputsDuringFileLoad)
                {
                    _sceneActionsQueue.Enqueue(stateMachineInput);
                }
            }
            _deferredSMInputsDuringFileLoad = null;
            _activeSourceFileLoader = null;
        }

        // State machine inputs to set once the current async file load finishes.
        private List<Action> _deferredSMInputsDuringFileLoad = null;

        private void EnqueueStateMachineInput(Action stateMachineInput)
        {
            if (_deferredSMInputsDuringFileLoad != null)
            {
                // A source file is currently loading async. Don't set this input until it completes.
                _deferredSMInputsDuringFileLoad.Add(stateMachineInput);
            }
            else
            {
                _sceneActionsQueue.Enqueue(stateMachineInput);
            }
        }

        public void SetBool(string name, bool value)
        {
            EnqueueStateMachineInput(() => _scene.SetBool(name, value));
        }

        public void SetNumber(string name, float value)
        {
            EnqueueStateMachineInput(() => _scene.SetNumber(name, value));
        }

        public void FireTrigger(string name)
        {
            EnqueueStateMachineInput(() => _scene.FireTrigger(name));
        }

        private delegate void PointerHandler(Vec2D pos);

        private void HandlePointerEvent(PointerHandler handler, PointerEventArgs e)
        {
            if (_activeSourceFileLoader != null)
            {
                // Ignore pointer events while a new scene is loading.
                return;
            }

            // Capture the viewSize and pointerPos at the time of the event.
            var viewSize = this.Bounds.Size;
            var pointerPos = e.GetCurrentPoint(this).Position;

            // Forward the pointer event to the render thread.
            _sceneActionsQueue.Enqueue(() =>
            {
                Mat2D mat = ComputeAlignment(viewSize.Width, viewSize.Height);
                if (mat.Invert(out var inverse))
                {
                    Vec2D artboardPos = inverse * new Vec2D((float)pointerPos.X, (float)pointerPos.Y);
                    handler(artboardPos);
                }
            });
        }

        // Incremented when the "InvalLoop" (responsible for scheduling PaintSurface events) should
        // terminate.
        int _invalLoopContinuationToken = 0;

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var observer = (this.VisualRoot as Control)?.GetObservable(Control.IsVisibleProperty);
            observer?.Subscribe(OnVisibilityChanged);
        }

        private void OnVisibilityChanged(bool visible)
        {
            ++_invalLoopContinuationToken;  // Terminate the existing inval loop (if any).
            if (visible)
            {
                Task.Run(async () => InvalLoopAsync(_invalLoopContinuationToken));
            }
        }

        // Schedules continual PaintSurface events at 120fps until the window is no longer visible.
        // (Multiple calls to Invalidate() between PaintSurface events are coalesced.)
        private async void InvalLoopAsync(int continuationToken)
        {
            while (continuationToken == _invalLoopContinuationToken)
            {
                await Dispatcher.UIThread.InvokeAsync(InvalidateVisual);
                await Task.Delay(TimeSpan.FromMilliseconds(8));  // 120 fps
            }
        }

        // _scene is used on the render thread exclusively.
        Scene _scene = new Scene();

        // Source actions originating from other threads must be funneled through this queue.
        readonly ConcurrentQueue<Action> _sceneActionsQueue = new ConcurrentQueue<Action>();

        // This is the render-thread copy of the animation parameters. They are set via
        // _sceneActionsQueue. _scene is then blah blah blah
        private string _artboardName;
        private string _animationName;
        private string _stateMachineName;

        private enum SceneUpdates
        {
            File = 3,
            Artboard = 2,
            AnimationOrStateMachine = 1,
        };

        DateTime _lastPaintTime;

        // Called from the render thread. Updates _scene according to updates.
        void UpdateScene(SceneUpdates updates, byte[] sourceFileData = null)
        {
            if (updates >= SceneUpdates.File)
            {
                _scene.LoadFile(sourceFileData);
            }
            if (updates >= SceneUpdates.Artboard)
            {
                _scene.LoadArtboard(_artboardName);
            }
            if (updates >= SceneUpdates.AnimationOrStateMachine)
            {
                if (!String.IsNullOrEmpty(_stateMachineName))
                {
                    _scene.LoadStateMachine(_stateMachineName);
                }
                else if (!String.IsNullOrEmpty(_animationName))
                {
                    _scene.LoadAnimation(_animationName);
                }
                else
                {
                    if (!_scene.LoadStateMachine(null))
                    {
                        _scene.LoadAnimation(null);
                    }
                }
            }
        }

        // Called from the render thread. Computes alignment based on the size of _scene.
        private Mat2D ComputeAlignment(double width, double height)
        {
            return ComputeAlignment(new AABB(0, 0, (float)width, (float)height));
        }

        // Called from the render thread. Computes alignment based on the size of _scene.
        private Mat2D ComputeAlignment(AABB frame)
        {
            return Renderer.ComputeAlignment(Fit.Contain, Alignment.Center, frame,
                                             new AABB(0, 0, _scene.Width, _scene.Height));
        }
    }
}
