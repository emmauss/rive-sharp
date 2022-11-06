// Copyright 2022 Rive

using Avalonia;
using Avalonia.Interactivity;
using Avalonia.Metadata;
using System;

namespace RiveSharp.Views
{
    // This base class wraps a custom, named state machine input value.
    public abstract class StateMachineInput : StyledElement
    {
        private string _target;
        public string Target
        {
            get => _target;  // Must be null-checked before use.
            set
            {
                _target = value;
                Apply();
            }
        }

        private WeakReference<RivePlayer> _rivePlayer = new WeakReference<RivePlayer>(null);
        protected WeakReference<RivePlayer> RivePlayer => _rivePlayer;

        // Sets _rivePlayer to the given rivePlayer object and applies our input value to the state
        // machine. Does nothing if _rivePlayer was already equal to rivePlayer.
        internal void SetRivePlayer(WeakReference<RivePlayer> rivePlayer)
        {
            _rivePlayer = rivePlayer;
            Apply();
        }

        protected void Apply()
        {
            if (!String.IsNullOrEmpty(_target) && _rivePlayer.TryGetTarget(out var rivePlayer))
            {
                Apply(rivePlayer, _target);
            }
        }

        // Applies our input value to the rivePlayer's state machine.
        // rivePlayer and inputName are guaranteed to not be null or empty.
        protected abstract void Apply(RivePlayer rivePlayer, string inputName);
    }

    public class BoolInput : StateMachineInput
    {
        // Define "Value" as a DependencyProperty so it can be data-bound.
        public static readonly StyledProperty<bool> ValueProperty = AvaloniaProperty.Register<BoolInput, bool>(
            nameof(Value));

        [Content]
        public bool Value
        {
            get => (bool)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        protected override void Apply(RivePlayer rivePlayer, string inputName)
        {
            rivePlayer.SetBool(inputName, this.Value);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == ValueProperty)
            {

                Apply();
            }
        }
    }

    public class NumberInput : StateMachineInput
    {
        // Define "Value" as a DependencyProperty so it can be data-bound.
        public static readonly StyledProperty<double> ValueProperty = AvaloniaProperty.Register<BoolInput, double>(
            nameof(Value));

        [Content]
        public double Value
        {
            get => (double)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        protected override void Apply(RivePlayer rivePlayer, string inputName)
        {
            rivePlayer.SetNumber(inputName, (float)this.Value);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == ValueProperty)
            {

                Apply();
            }
        }
    }

    public class TriggerInput : StateMachineInput
    {
        public void Fire()
        {
            if (!String.IsNullOrEmpty(this.Target) && this.RivePlayer.TryGetTarget(out var rivePlayer))
            {
                rivePlayer.FireTrigger(this.Target);
            }
        }

        // Make a Fire() overload that matches the RoutedEventHandler delegate.
        // This allows us do to things like <Button Click="MyTriggerInput.Fire" ... />
        public void Fire(object s, RoutedEventArgs e) => Fire();

        // Triggers don't have any persistent data to apply.
        protected override void Apply(RivePlayer rivePlayer, string inputName) { }
    }
}
