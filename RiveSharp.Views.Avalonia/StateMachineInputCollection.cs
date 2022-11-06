// Copyright 2022 Rive

using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace RiveSharp.Views
{
    // Manages a collection of StateMachineInput objects for RivePlayer. The [ContentProperty] tag
    // on RivePlayer instructs the XAML engine automatically route nested inputs through this
    // collection:
    //
    //   <rive:RivePlayer Source="...">
    //       <rive:BoolInput Target=... />
    //   </rive:RivePlayer>
    //
    public class StateMachineInputCollection : ObservableCollection<StateMachineInput>
    {
        private readonly WeakReference<RivePlayer> rivePlayer;

        public StateMachineInputCollection(RivePlayer rivePlayer)
        {
            this.rivePlayer = new WeakReference<RivePlayer>(rivePlayer);
            CollectionChanged += StateMachineInputCollection_CollectionChanged;
        }

        private void StateMachineInputCollection_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                case NotifyCollectionChangedAction.Replace:
                case NotifyCollectionChangedAction.Move:
                {
                    var input = this[e.NewStartingIndex];
                    input.SetRivePlayer(rivePlayer);
                }
                    break;
                case NotifyCollectionChangedAction.Remove:
                {
                    var input = (StateMachineInput)this[e.OldStartingIndex];
                    input.SetRivePlayer(new WeakReference<RivePlayer>(null));
                }
                    break;
                case NotifyCollectionChangedAction.Reset:
                    foreach (StateMachineInput input in this)
                    {
                        input.SetRivePlayer(new WeakReference<RivePlayer>(null));
                    }
                    break;
            }
        }
    }
}
