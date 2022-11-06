// Copyright 2022 Rive

using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;
using System;

namespace RiveSharp.Views
{
    public class CustomDrawing : ICustomDrawOperation
    {
        private readonly Action<SKCanvas> _drawAction;

        public Control Control { get; }

        public CustomDrawing(Control control, Action<SKCanvas> drawAction)
        {
            Control = control;
            _drawAction = drawAction;
        }

        public Rect Bounds
        {
            get => new Rect(0, 0, Control.Bounds.Width, Control.Bounds.Height);
        }

        public void Dispose()
        {
        }

        public bool Equals(ICustomDrawOperation other)
        {
            return false;
        }

        public bool HitTest(Point p)
        {
            return Bounds.Contains(p);
        }

        public void Render(IDrawingContextImpl context)
        {
            var leaseFeature = context?.GetFeature<ISkiaSharpApiLeaseFeature>();

            if (leaseFeature == null)
            {
                return;
            }
            using var lease = leaseFeature.Lease();

            _drawAction?.Invoke(lease.SkCanvas);
        }
    }
}
